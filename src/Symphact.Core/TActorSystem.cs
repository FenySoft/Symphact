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
public sealed class TActorSystem : IDisposable
{
    private const int CInitialCapacity = 16;

    private readonly IPlatform FPlatform;
    private TActorEntry?[] FSlots = new TActorEntry?[CInitialCapacity];
    private int FNextSlotIndex;
    private int FActorCount;
    private bool FDisposed;

    /// <summary>
    /// hu: Létrehoz egy aktor rendszert a megadott platform implementációval.
    /// <br />
    /// en: Creates an actor system with the given platform implementation.
    /// </summary>
    /// <param name="APlatform">
    /// hu: A hardver platform absztrakció (mailbox gyár, core management).
    /// <br />
    /// en: The hardware platform abstraction (mailbox factory, core management).
    /// </param>
    public TActorSystem(IPlatform APlatform)
    {
        ArgumentNullException.ThrowIfNull(APlatform);
        FPlatform = APlatform;
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
        entry.PreStart();

        return new TActorRef(id);
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

        // Cascade: stop all children first (depth-first)
        for (var i = 0; i < AEntry.Children.Count; i++)
        {
            var childEntry = GetEntry(AEntry.Children[i]);

            if (childEntry is not null)
                StopActor(childEntry);
        }

        AEntry.PostStop();
        NotifyWatchers(AEntry);
    }

    private void NotifyWatchers(TActorEntry AEntry)
    {
        var stoppedRef = new TActorRef(AEntry.SlotIndex);

        foreach (var watcherRef in AEntry.Watchers)
        {
            var watcherEntry = GetEntry(watcherRef);

            if (watcherEntry is not null && !watcherEntry.Stopped)
                watcherEntry.Mailbox.Post(new TTerminated(stoppedRef));
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
                watcherEntry.Mailbox.Post(new TTerminated(ATarget));

            return;
        }

        targetEntry.Watchers.Add(AWatcher);
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
        targetEntry?.Watchers.Remove(AWatcher);
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

        return entry.Children;
    }

    /// <summary>
    /// hu: Leállítja a rendszert. A további Send/Spawn/GetState hívások ObjectDisposedException-t dobnak.
    /// <br />
    /// en: Shuts down the system. Further Send/Spawn/GetState calls raise ObjectDisposedException.
    /// </summary>
    public void Dispose()
    {
        if (FDisposed)
            return;

        FDisposed = true;
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
        parentEntry?.Children.Add(new TActorRef(id));

        entry.PreStart();

        return new TActorRef(id);
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
        EnsureCapacity(ASlotIndex);
        FSlots[ASlotIndex] = AEntry;
        FActorCount++;
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
        public HashSet<TActorRef> Watchers { get; } = new();
        public bool Stopped { get; set; }
    }
}
