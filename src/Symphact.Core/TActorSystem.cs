using System.Collections.Concurrent;

namespace Symphact.Core;

/// <summary>
/// hu: Az aktor runtime fő belépési pontja. Ez egy referencia, single-host implementáció —
/// egyelőre nem multi-core, nem distributed, és nincs supervision (azok későbbi iterációk).
/// Spawn-olja az aktorokat, mailbox-ot ad mindegyiknek, és üzenet-feldolgozó loop-ot futtat.
/// Lekéri az aktor aktuális állapotát (teszteléshez). A DrainAsync addig dolgozza fel az
/// üzeneteket, amíg minden mailbox ki nem ürül.
/// <br />
/// en: Main entry point to the actor runtime. This is a reference single-host implementation —
/// not multi-core, not distributed, no supervision (those are future iterations). Spawns actors,
/// gives each a mailbox, and runs a message-processing loop. Exposes actor state for testing.
/// DrainAsync processes messages until every mailbox is empty.
/// </summary>
public sealed class TActorSystem : IDisposable
{
    private readonly ConcurrentDictionary<long, TActorEntry> FActors = new();
    private long FNextActorId;
    private bool FDisposed;

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

        var id = Interlocked.Increment(ref FNextActorId);
        var actor = new TActorType();
        var entry = new TActorEntry(actor, actor.Init()!, (state, msg, ctx) => actor.Handle((TState)state, msg, ctx)!);

        FActors[id] = entry;
        return new TActorRef(id);
    }

    /// <summary>
    /// hu: Egy üzenet elhelyezése a cél aktor mailboxában. Thread-safe; nem blokkol.
    /// Az üzenet feldolgozása a DrainAsync hívásakor történik.
    /// <br />
    /// en: Places a message in the target actor's mailbox. Thread-safe; non-blocking.
    /// Actual processing happens on DrainAsync.
    /// </summary>
    public void Send(TActorRef ATarget, object AMessage)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(AMessage);

        if (!ATarget.IsValid || !FActors.TryGetValue(ATarget.ActorId, out var entry))
            throw new InvalidOperationException($"Invalid actor reference: {ATarget}");

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
    public Task DrainAsync(int AMaxRounds = 1000)
    {
        ThrowIfDisposed();

        var rounds = 0;
        bool anyProcessed;

        do
        {
            if (++rounds > AMaxRounds)
                throw new InvalidOperationException(
                    $"DrainAsync did not converge within {AMaxRounds} rounds. " +
                    "Possible cause: actors are sending messages in an infinite cycle.");

            anyProcessed = false;

            foreach (var (actorId, entry) in FActors)
            {
                var context = new TActorContext(this, new TActorRef(actorId));

                while (entry.Mailbox.TryReceive(out var message))
                {
                    entry.State = entry.Handler(entry.State, message!, context);
                    anyProcessed = true;
                }
            }
        }
        while (anyProcessed);

        return Task.CompletedTask;
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

        if (!FActors.TryGetValue(AActor.ActorId, out var entry))
            throw new InvalidOperationException($"Unknown actor: {AActor}");

        return (TState)entry.State;
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
        FActors.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (FDisposed)
            throw new ObjectDisposedException(nameof(TActorSystem));
    }

    private sealed class TActorEntry
    {
        public TActorEntry(object AActor, object AInitialState, Func<object, object, IActorContext, object> AHandler)
        {
            Actor = AActor;
            State = AInitialState;
            Handler = AHandler;
            Mailbox = new TMailbox();
        }

        public object Actor { get; }
        public object State { get; set; }
        public Func<object, object, IActorContext, object> Handler { get; }
        public IMailbox Mailbox { get; }
    }
}
