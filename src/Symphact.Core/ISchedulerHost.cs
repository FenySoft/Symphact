namespace Symphact.Core;

/// <summary>
/// hu: Az IScheduler → TActorSystem visszahívási felület (M0.4). Az aktor handler hívása
/// a host (TActorSystem) felelőssége — a scheduler csak annyit ismer ebből, hogy "futtass
/// egy szeletet az adott aktorra, és mondd meg, történt-e bármi". A supervision logikát
/// (HandleFailure, Restart, Stop, Escalate) a host helyben, ugyanazon a thread-en intézi,
/// ahol a slice exception-t fogott — cross-thread mailbox-injekció (TFailureEnvelope) NINCS.
/// <br />
/// en: The IScheduler → TActorSystem callback surface (M0.4). Calling the actor handler is
/// the host's (TActorSystem's) responsibility — the scheduler only knows "run a slice on
/// this actor and tell me if anything happened." Supervision logic (HandleFailure, Restart,
/// Stop, Escalate) is performed by the host on the same thread the slice trapped on —
/// there is NO cross-thread mailbox injection (no TFailureEnvelope).
/// </summary>
public interface ISchedulerHost
{
    /// <summary>
    /// hu: Az adott aktor maximum AMaxMessages üzenetét feldolgozza, single-threaded
    /// invariáns mellett (egy aktorhoz egy thread egyszerre). A scheduler köteles
    /// biztosítani, hogy ugyanarra az aktorra egyszerre csak egy thread hívja meg ezt
    /// a metódust — különben a "Single-threaded actor" invariáns sérül.
    /// <br />
    /// en: Processes up to AMaxMessages messages for the given actor, under the
    /// single-threaded actor invariant (only one thread per actor at a time). The scheduler
    /// MUST ensure that no two threads call this method for the same actor concurrently —
    /// otherwise the "single-threaded actor" invariant is violated.
    /// </summary>
    /// <param name="AActor">
    /// hu: A futtatandó aktor referenciája.
    /// <br />
    /// en: Reference of the actor whose slice should run.
    /// </param>
    /// <param name="AMaxMessages">
    /// hu: A slice során feldolgozandó maximum üzenetszám. Round-robin fairness-hez használt.
    /// 0 vagy negatív → az implementáció dönt (jellemzően minden várakozó üzenetet).
    /// <br />
    /// en: Maximum messages to process in the slice. Used for round-robin fairness. Zero or
    /// negative → implementation decides (typically all queued messages).
    /// </param>
    /// <returns>
    /// hu: true ha legalább egy üzenet feldolgozódott; false ha a mailbox üres volt.
    /// <br />
    /// en: true if at least one message was processed; false if the mailbox was empty.
    /// </returns>
    bool RunOneSlice(TActorRef AActor, int AMaxMessages);

    /// <summary>
    /// hu: Megadja, hogy az aktor jelenleg idle-e — mailbox üres ÉS nincs futó slice.
    /// A scheduler quiescence-detekciójához használt segédinformáció.
    /// <br />
    /// en: Returns whether the actor is currently idle — mailbox is empty AND no slice is
    /// running. Used by the scheduler's quiescence detection.
    /// </summary>
    /// <param name="AActor">
    /// hu: A vizsgálandó aktor referenciája.
    /// <br />
    /// en: Reference of the actor to inspect.
    /// </param>
    /// <returns>
    /// hu: true ha az aktor idle (lehet, hogy le van állítva is); false ha üzenet vár vagy
    /// éppen fut.
    /// <br />
    /// en: true if the actor is idle (may also be stopped); false if a message is queued or
    /// a slice is currently running.
    /// </returns>
    bool IsActorIdle(TActorRef AActor);
}
