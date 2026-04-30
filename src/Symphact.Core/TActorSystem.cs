namespace Symphact.Core;

/// <summary>
/// hu: Az aktor runtime fő belépési pontja. Ez egy referencia, single-host implementáció.
/// A belső FSlots tömb a CFPU Capability Slot Table (CST) szoftveres tükrözése — a SlotIndex
/// közvetlenül array index, O(1) elérés, zero hashing, cache-barát.
/// <br />
/// en: Main entry point to the actor runtime. This is a reference single-host implementation.
/// The internal FSlots array mirrors the CFPU Capability Slot Table (CST) — SlotIndex maps
/// directly to an array index, O(1) access, zero hashing, cache-friendly.
/// </summary>
public sealed class TActorSystem : IDisposable, ISchedulerHost
{
    private const int CInitialCapacity = 16;

    private readonly IPlatform FPlatform;
    private readonly IScheduler FScheduler;
    private readonly object FSlotsLock = new();
    private TActorEntry?[] FSlots = new TActorEntry?[CInitialCapacity];
    private int FNextSlotIndex;
    private int FActorCount;
    private volatile bool FDisposed;

    /// <summary>
    /// hu: Létrehoz egy aktor rendszert a megadott platform implementációval. Az aktor
    /// rendszer alapértelmezett ütemezője a TInlineScheduler (szinkron, single-threaded).
    /// Multi-thread parallelizmushoz használd a két paraméteres konstruktort egyedi
    /// schedulerrel (pl. TDedicatedThreadScheduler).
    /// <br />
    /// en: Creates an actor system with the given platform implementation. The default
    /// scheduler is TInlineScheduler (synchronous, single-threaded). For multi-threaded
    /// parallelism use the two-argument constructor with a custom scheduler such as
    /// TDedicatedThreadScheduler.
    /// </summary>
    /// <param name="APlatform">
    /// hu: A hardver platform absztrakció (mailbox gyár, core management).
    /// <br />
    /// en: The hardware platform abstraction (mailbox factory, core management).
    /// </param>
    public TActorSystem(IPlatform APlatform)
        : this(APlatform, new TInlineScheduler())
    {
    }

    /// <summary>
    /// hu: Létrehoz egy aktor rendszert a megadott platformmal és egyedi schedulerrel
    /// (M0.4). A scheduler az aktor system tulajdonába kerül — a Dispose lezárja.
    /// <br />
    /// en: Creates an actor system with the given platform and a custom scheduler
    /// (M0.4). The scheduler is owned by the actor system — Dispose closes it.
    /// </summary>
    /// <param name="APlatform">
    /// hu: A hardver platform absztrakció.
    /// <br />
    /// en: The hardware platform abstraction.
    /// </param>
    /// <param name="AScheduler">
    /// hu: Az ütemező implementáció. A rendszer Attach-olja és Dispose-olja.
    /// <br />
    /// en: The scheduler implementation. The system attaches to it and disposes it.
    /// </param>
    public TActorSystem(IPlatform APlatform, IScheduler AScheduler)
    {
        ArgumentNullException.ThrowIfNull(APlatform);
        ArgumentNullException.ThrowIfNull(AScheduler);

        FPlatform = APlatform;
        FScheduler = AScheduler;
        FScheduler.Attach(this);
    }

    /// <summary>
    /// hu: Létrehoz és elindít egy új aktort a megadott típussal. Az aktor az Init-et hívja
    /// a kezdőállapot meghatározásához, és azonnal fogadhat üzeneteket a Send-del.
    /// <br />
    /// en: Creates and starts a new actor of the given type. The actor's Init is called to
    /// determine the initial state, and it can immediately receive messages via Send.
    /// </summary>
    public TActorRef Spawn<TActorType, TState>()
        where TActorType : TActor<TState>, new()
    {
        ThrowIfDisposed();

        var id = Interlocked.Increment(ref FNextSlotIndex);
        var actor = new TActorType();
        var entry = CreateEntry<TActorType, TState>(id, actor, TActorRef.Invalid);

        SetSlot(id, entry);
        var actorRef = new TActorRef(id);
        FScheduler.Register(actorRef, entry.Mailbox);
        entry.PreStart();

        return actorRef;
    }

    /// <summary>
    /// hu: Egy üzenet elhelyezése a cél aktor mailboxában. Thread-safe; nem blokkol.
    /// Az üzenet feldolgozása a Drain hívásakor történik.
    /// <br />
    /// en: Places a message in the target actor's mailbox. Thread-safe; non-blocking.
    /// Actual processing happens on Drain.
    /// </summary>
    public void Send(TActorRef ATarget, object AMessage)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(AMessage);

        var entry = GetEntry(ATarget);

        if (entry is null)
            throw new InvalidOperationException($"Invalid actor reference: {ATarget}");

        if (entry.Stopped)
            return;

        entry.Mailbox.Post(AMessage);
        FScheduler.Signal(ATarget);
    }

    /// <summary>
    /// hu: Feldolgozza az összes aktor összes várakozó üzenetét. Mivel az aktorok üzeneteket
    /// küldhetnek egymásnak, több fordulót futtathat, amíg minden mailbox üres. Ha a körök
    /// száma eléri az AMaxRounds korlátot, InvalidOperationException keletkezik (végtelen loop
    /// védelem). Egyszerű, single-threaded drain — later iterációkban core-onkénti szálak jönnek.
    /// <br />
    /// en: Processes every pending message on every actor. Because actors may send messages
    /// to each other, this may iterate multiple rounds until all mailboxes are empty. If the
    /// round count reaches AMaxRounds, throws InvalidOperationException (infinite-loop guard).
    /// Simple single-threaded drain — per-core threads arrive in later iterations.
    /// </summary>
    /// <param name="AMaxRounds">
    /// hu: Maximálisan megengedett körök száma. Ha eléri, InvalidOperationException keletkezik.
    /// Alapértelmezett: 1000 — tipikus tesztekhez és lineáris lánc-feldolgozáshoz elegendő.
    /// <br />
    /// en: Maximum allowed rounds. If reached, throws InvalidOperationException.
    /// Default: 1000 — sufficient for typical tests and linear chain processing.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// hu: Ha az üzenetfeldolgozás nem konvergál AMaxRounds körön belül (pl. végtelen ping-pong).
    /// <br />
    /// en: If message processing does not converge within AMaxRounds rounds (e.g. infinite ping-pong).
    /// </exception>
    public void Drain(int AMaxRounds = 1000)
    {
        ThrowIfDisposed();

        var rounds = 0;
        bool anyProcessed;

        do
        {
            if (++rounds > AMaxRounds)
                throw new InvalidOperationException(
                    $"Drain did not converge within {AMaxRounds} rounds. " +
                    "Possible cause: actors are sending messages in an infinite cycle.");

            anyProcessed = false;

            var count = FNextSlotIndex;

            for (var slotIndex = 1; slotIndex <= count; slotIndex++)
            {
                var entry = slotIndex < FSlots.Length ? FSlots[slotIndex] : null;

                if (entry is null || entry.Stopped)
                    continue;

                var context = new TActorContext(this, new TActorRef(slotIndex));

                while (entry.Mailbox.TryReceive(out var message))
                {
                    if (entry.Stopped)
                        break;

                    anyProcessed = true;

                    try
                    {
                        entry.State = entry.Handler(entry.State, message!, context);
                    }
                    catch (Exception ex)
                    {
                        HandleFailure(new TActorRef(slotIndex), entry, ex);
                        break;
                    }
                }
            }
        }
        while (anyProcessed);
    }

    private void HandleFailure(TActorRef AChildRef, TActorEntry AChildEntry, Exception AException)
    {
        var parentRef = AChildEntry.Parent;
        var parentEntry = GetEntry(parentRef);

        // Root actor (no parent) — exception escapes (preserves pre-supervision behaviour)
        if (parentEntry is null)
            throw AException;

        var strategy = parentEntry.GetSupervisorStrategy();

        // No strategy on parent — escalate (throw)
        if (strategy is null)
            throw AException;

        var directive = strategy.Decide(AChildRef, AException);

        switch (directive)
        {
            case ESupervisorDirective.Resume:
                // Continue with current state — the failing message is lost
                break;

            case ESupervisorDirective.Restart:
                RestartActor(AChildEntry, AException);
                break;

            case ESupervisorDirective.Stop:
                StopActor(AChildEntry);
                break;

            case ESupervisorDirective.Escalate:
                // Re-throw as if the parent itself failed
                throw AException;

            default:
                throw new InvalidOperationException($"Unknown supervisor directive: {directive}");
        }
    }

    private void RestartActor(TActorEntry AEntry, Exception AException)
    {
        AEntry.PreRestart(AException);
        AEntry.State = AEntry.Init();
        AEntry.PostRestart(AException);
    }

    private void StopActor(TActorEntry AEntry)
    {
        if (AEntry.Stopped)
            return;

        AEntry.Stopped = true;

        // Cascade: stop all children first (depth-first). Snapshot children under lock to
        // avoid concurrent-modification races against SpawnChild on another thread.
        TActorRef[] childrenSnapshot;

        lock (AEntry.ChildrenLock)
        {
            childrenSnapshot = AEntry.Children.ToArray();
        }

        for (var i = 0; i < childrenSnapshot.Length; i++)
        {
            var childEntry = GetEntry(childrenSnapshot[i]);

            if (childEntry is not null)
                StopActor(childEntry);
        }

        AEntry.PostStop();
        NotifyWatchers(AEntry);
        FScheduler.Unregister(new TActorRef(AEntry.SlotIndex));
    }

    private void NotifyWatchers(TActorEntry AEntry)
    {
        var stoppedRef = new TActorRef(AEntry.SlotIndex);

        TActorRef[] watcherSnapshot;

        lock (AEntry.WatchersLock)
        {
            watcherSnapshot = new TActorRef[AEntry.Watchers.Count];
            AEntry.Watchers.CopyTo(watcherSnapshot);
        }

        foreach (var watcherRef in watcherSnapshot)
        {
            var watcherEntry = GetEntry(watcherRef);

            if (watcherEntry is not null && !watcherEntry.Stopped)
            {
                watcherEntry.Mailbox.Post(new TTerminated(stoppedRef));
                FScheduler.Signal(watcherRef);
            }
        }
    }

    /// <summary>
    /// hu: Egy aktor figyelésének regisztrálása. Belső metódus — a TActorContext hívja.
    /// <br />
    /// en: Register an actor watch. Internal method — called by TActorContext.
    /// </summary>
    internal void WatchActor(TActorRef AWatcher, TActorRef ATarget)
    {
        ThrowIfDisposed();

        var targetEntry = GetEntry(ATarget);

        if (targetEntry is null)
            return;

        // If already stopped, deliver TTerminated immediately
        if (targetEntry.Stopped)
        {
            var watcherEntry = GetEntry(AWatcher);

            if (watcherEntry is not null && !watcherEntry.Stopped)
            {
                watcherEntry.Mailbox.Post(new TTerminated(ATarget));
                FScheduler.Signal(AWatcher);
            }

            return;
        }

        lock (targetEntry.WatchersLock)
        {
            targetEntry.Watchers.Add(AWatcher);
        }
    }

    /// <summary>
    /// hu: Egy aktor figyelésének eltávolítása. Belső metódus — a TActorContext hívja.
    /// <br />
    /// en: Remove an actor watch. Internal method — called by TActorContext.
    /// </summary>
    internal void UnwatchActor(TActorRef AWatcher, TActorRef ATarget)
    {
        ThrowIfDisposed();

        var targetEntry = GetEntry(ATarget);

        if (targetEntry is null)
            return;

        lock (targetEntry.WatchersLock)
        {
            targetEntry.Watchers.Remove(AWatcher);
        }
    }

    /// <summary>
    /// hu: Visszaadja egy aktor aktuális állapotát (teszt/diagnosztika célra). Egy éles
    /// rendszerben az aktor állapota privát — csak üzenet-alapon elérhető.
    /// <br />
    /// en: Returns the current state of an actor (for testing/diagnostics). In a production
    /// setting the state is private — accessible only via messages.
    /// </summary>
    public TState GetState<TState>(TActorRef AActor)
    {
        ThrowIfDisposed();

        var entry = GetEntry(AActor)
            ?? throw new InvalidOperationException($"Unknown actor: {AActor}");

        return (TState)entry.State;
    }

    /// <summary>
    /// hu: Visszaadja egy aktor szülőjét (teszt/diagnosztika célra). Root aktoroknak Invalid.
    /// <br />
    /// en: Returns the parent of an actor (for testing/diagnostics). Root actors return Invalid.
    /// </summary>
    public TActorRef GetParent(TActorRef AActor)
    {
        ThrowIfDisposed();

        var entry = GetEntry(AActor)
            ?? throw new InvalidOperationException($"Unknown actor: {AActor}");

        return entry.Parent;
    }

    /// <summary>
    /// hu: Megadja, hogy egy aktor le van-e állítva (teszt/diagnosztika célra).
    /// <br />
    /// en: Returns whether an actor is stopped (for testing/diagnostics).
    /// </summary>
    public bool IsStopped(TActorRef AActor)
    {
        ThrowIfDisposed();

        var entry = GetEntry(AActor)
            ?? throw new InvalidOperationException($"Unknown actor: {AActor}");

        return entry.Stopped;
    }

    /// <summary>
    /// hu: Visszaadja egy aktor gyerekeinek listáját (teszt/diagnosztika célra).
    /// <br />
    /// en: Returns the list of an actor's children (for testing/diagnostics).
    /// </summary>
    public IReadOnlyList<TActorRef> GetChildren(TActorRef AActor)
    {
        ThrowIfDisposed();

        var entry = GetEntry(AActor)
            ?? throw new InvalidOperationException($"Unknown actor: {AActor}");

        lock (entry.ChildrenLock)
        {
            return entry.Children.ToArray();
        }
    }

    /// <summary>
    /// hu: Aszinkron várakozás a quiescence-re a rendszer minden aktora fölött (M0.4). Az
    /// ütemezőn keresztül delegálja a barriert; a hívás visszatérése után minden eddig
    /// küldött üzenet feldolgozódott (vagy a megfelelő aktor leállt). Ha a megadott időn
    /// belül nem áll be a quiescence, TimeoutException-t dob (nem deadlockol).
    /// <br />
    /// en: Asynchronously wait for quiescence over all actors in the system (M0.4). Delegates
    /// the barrier to the underlying scheduler; after the call returns, every message sent
    /// so far has been processed (or its actor is stopped). If quiescence is not reached
    /// within the timeout, throws TimeoutException (does not deadlock).
    /// </summary>
    /// <param name="ATimeout">
    /// hu: Maximum várakozási idő. Lejárat után TimeoutException.
    /// <br />
    /// en: Maximum wait time. On expiry throws TimeoutException.
    /// </param>
    /// <param name="ACancellationToken">
    /// hu: Megszakítási token.
    /// <br />
    /// en: Cancellation token.
    /// </param>
    public Task QuiesceAsync(TimeSpan ATimeout, CancellationToken ACancellationToken = default)
    {
        ThrowIfDisposed();

        return FScheduler.QuiesceAsync(ATimeout, ACancellationToken);
    }

    /// <summary>
    /// hu: Leállítja a rendszert. A további Send/Spawn/GetState hívások ObjectDisposedException-t dobnak.
    /// Az ütemezőt is lezárja.
    /// <br />
    /// en: Shuts down the system. Further Send/Spawn/GetState calls raise ObjectDisposedException.
    /// Also disposes the scheduler.
    /// </summary>
    public void Dispose()
    {
        if (FDisposed)
            return;

        FDisposed = true;
        FScheduler.Dispose();
        Array.Clear(FSlots);
        FActorCount = 0;
    }

    /// <summary>
    /// hu: Gyerek aktor létrehozása a megadott szülőhöz. Belső metódus — a TActorContext hívja.
    /// <br />
    /// en: Spawn a child actor for the given parent. Internal method — called by TActorContext.
    /// </summary>
    internal TActorRef SpawnChild<TActorType, TState>(TActorRef AParent)
        where TActorType : TActor<TState>, new()
    {
        ThrowIfDisposed();

        var id = Interlocked.Increment(ref FNextSlotIndex);
        var actor = new TActorType();
        var entry = CreateEntry<TActorType, TState>(id, actor, AParent);

        SetSlot(id, entry);

        var parentEntry = GetEntry(AParent);
        var actorRef = new TActorRef(id);

        if (parentEntry is not null)
        {
            lock (parentEntry.ChildrenLock)
            {
                parentEntry.Children.Add(actorRef);
            }
        }

        FScheduler.Register(actorRef, entry.Mailbox);
        entry.PreStart();

        return actorRef;
    }

    private void ThrowIfDisposed()
    {
        if (FDisposed)
            throw new ObjectDisposedException(nameof(TActorSystem));
    }

    private TActorEntry? GetEntry(TActorRef ARef)
    {
        var idx = ARef.SlotIndex;

        if (idx <= 0 || idx >= FSlots.Length)
            return null;

        return FSlots[idx];
    }

    private void SetSlot(int ASlotIndex, TActorEntry AEntry)
    {
        lock (FSlotsLock)
        {
            EnsureCapacity(ASlotIndex);
            FSlots[ASlotIndex] = AEntry;
            FActorCount++;
        }
    }

    private void EnsureCapacity(int ASlotIndex)
    {
        if (ASlotIndex < FSlots.Length)
            return;

        var newCapacity = FSlots.Length;

        while (newCapacity <= ASlotIndex)
            newCapacity *= 2;

        var newSlots = new TActorEntry?[newCapacity];
        Array.Copy(FSlots, newSlots, FSlots.Length);
        FSlots = newSlots;
    }

    /// <inheritdoc />
    bool ISchedulerHost.RunOneSlice(TActorRef AActor, int AMaxMessages)
    {
        if (FDisposed)
            return false;

        var entry = GetEntry(AActor);

        if (entry is null || entry.Stopped)
            return false;

        var context = new TActorContext(this, AActor);
        var processed = false;
        var count = 0;

        while (entry.Mailbox.TryReceive(out var message))
        {
            if (entry.Stopped)
                break;

            processed = true;

            try
            {
                entry.State = entry.Handler(entry.State, message!, context);
            }
            catch (Exception ex)
            {
                HandleFailure(AActor, entry, ex);
                break;
            }

            count++;

            if (AMaxMessages > 0 && count >= AMaxMessages)
                break;
        }

        return processed;
    }

    /// <inheritdoc />
    bool ISchedulerHost.IsActorIdle(TActorRef AActor)
    {
        if (FDisposed)
            return true;

        var entry = GetEntry(AActor);

        if (entry is null || entry.Stopped)
            return true;

        return entry.Mailbox.Count == 0;
    }

    private TActorEntry CreateEntry<TActorType, TState>(int ASlotIndex, TActorType AActor, TActorRef AParent)
        where TActorType : TActor<TState>
    {
        return new TActorEntry(
            ASlotIndex,
            AActor.Init()!,
            (state, msg, ctx) => AActor.Handle((TState)state, msg, ctx)!,
            () => AActor.Init()!,
            AActor.PreStart,
            AActor.PostStop,
            AActor.PreRestart,
            AActor.PostRestart,
            () => AActor.SupervisorStrategy,
            FPlatform.CreateMailbox())
        {
            Parent = AParent
        };
    }

    private sealed class TActorEntry
    {
        private volatile bool FStopped;

        public TActorEntry(
            int ASlotIndex,
            object AInitialState,
            Func<object, object, IActorContext, object> AHandler,
            Func<object> AInit,
            Action APreStart,
            Action APostStop,
            Action<Exception> APreRestart,
            Action<Exception> APostRestart,
            Func<ISupervisorStrategy?> AGetSupervisorStrategy,
            IMailbox AMailbox)
        {
            SlotIndex = ASlotIndex;
            State = AInitialState;
            Handler = AHandler;
            Init = AInit;
            PreStart = APreStart;
            PostStop = APostStop;
            PreRestart = APreRestart;
            PostRestart = APostRestart;
            GetSupervisorStrategy = AGetSupervisorStrategy;
            Mailbox = AMailbox;
        }

        public int SlotIndex { get; }
        public object State { get; set; }
        public Func<object, object, IActorContext, object> Handler { get; }
        public Func<object> Init { get; }
        public Action PreStart { get; }
        public Action PostStop { get; }
        public Action<Exception> PreRestart { get; }
        public Action<Exception> PostRestart { get; }
        public Func<ISupervisorStrategy?> GetSupervisorStrategy { get; }
        public IMailbox Mailbox { get; }
        public TActorRef Parent { get; init; } = TActorRef.Invalid;
        public List<TActorRef> Children { get; } = new();
        public object ChildrenLock { get; } = new();
        public HashSet<TActorRef> Watchers { get; } = new();
        public object WatchersLock { get; } = new();

        public bool Stopped
        {
            get => FStopped;
            set => FStopped = value;
        }
    }
}
