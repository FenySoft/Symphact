namespace Symphact.Core;

/// <summary>
/// hu: Egy aktor futtatási kontextusa, amelyet a Handle metódus kap meg üzenet-feldolgozáskor.
/// Hozzáférést biztosít az aktor saját referenciájához (Self) és az üzenetküldési képességhez
/// (Send). Ez a CFPU actor-context regiszter szoftveres megfelelője — minden mag kap egy
/// kontextust, amely tartalmazza az azonosítóját és a mailbox FIFO elérési útját.
/// <br />
/// FONTOS: A kontextus kizárólag a Handle hívás ideje alatt érvényes. Ne tárold az aktor
/// állapotában (state-ben) — a Handle visszatérése után a kontextus érvénytelen lehet.
/// <br />
/// en: The execution context of an actor, received by Handle during message processing.
/// Provides access to the actor's own reference (Self) and the message-sending capability
/// (Send). This is the software equivalent of the CFPU actor-context register — each core
/// receives a context containing its identity and mailbox FIFO access path.
/// <br />
/// IMPORTANT: The context is only valid for the duration of the Handle call. Do not store
/// it in actor state — after Handle returns the context may be invalid.
/// </summary>
public interface IActorContext
{
    /// <summary>
    /// hu: Az aktuálisan futó aktor referenciája. Felhasználható, hogy az aktor megossza
    /// saját elérhetőségét más aktorokkal.
    /// <br />
    /// en: The reference of the currently executing actor. May be used to share the actor's
    /// own address with other actors.
    /// </summary>
    TActorRef Self { get; }

    /// <summary>
    /// hu: Üzenet küldése egy másik aktornak. Thread-safe; nem blokkol; az üzenet a cél
    /// mailboxában landol, és a soron következő DrainAsync-ban kerül feldolgozásra.
    /// <br />
    /// en: Send a message to another actor. Thread-safe; non-blocking; the message lands
    /// in the target's mailbox and is processed in the next DrainAsync pass.
    /// </summary>
    /// <param name="ATarget">
    /// hu: A cél aktor referenciája. Érvénytelen ref esetén InvalidOperationException.
    /// <br />
    /// en: The target actor reference. Throws InvalidOperationException for an invalid ref.
    /// </param>
    /// <param name="AMessage">
    /// hu: A küldendő üzenet. Nem lehet null.
    /// <br />
    /// en: The message to send. Must not be null.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// hu: Ha az ATarget érvénytelen vagy ismeretlen.
    /// <br />
    /// en: If ATarget is invalid or unknown.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// hu: Ha az AMessage null.
    /// <br />
    /// en: If AMessage is null.
    /// </exception>
    void Send(TActorRef ATarget, object AMessage);

    /// <summary>
    /// hu: Gyerek aktor létrehozása az aktuális aktorból. A gyerek szülője az aktuálisan futó
    /// aktor lesz — ez a szülő-gyerek hierarchia alapja a supervision-höz.
    /// <br />
    /// en: Spawn a child actor from the current actor. The child's parent will be the currently
    /// executing actor — this is the foundation of parent-child hierarchy for supervision.
    /// </summary>
    /// <typeparam name="TActorType">
    /// hu: Az aktor típusa.
    /// <br />
    /// en: The actor type.
    /// </typeparam>
    /// <typeparam name="TState">
    /// hu: Az aktor állapot típusa.
    /// <br />
    /// en: The actor state type.
    /// </typeparam>
    /// <returns>
    /// hu: Az új gyerek aktor referenciája.
    /// <br />
    /// en: The new child actor's reference.
    /// </returns>
    TActorRef Spawn<TActorType, TState>()
        where TActorType : TActor<TState>, new();

    /// <summary>
    /// hu: Egy másik aktor figyelése. Ha a figyelt aktor megáll, a figyelő TTerminated üzenetet
    /// kap a mailboxába. Ha a cél már leállt, azonnal kézbesít.
    /// <br />
    /// en: Watch another actor. When the watched actor stops, the watcher receives a TTerminated
    /// message in its mailbox. If the target is already stopped, delivers immediately.
    /// </summary>
    /// <param name="ATarget">
    /// hu: A figyelendő aktor referenciája.
    /// <br />
    /// en: The reference of the actor to watch.
    /// </param>
    void Watch(TActorRef ATarget);

    /// <summary>
    /// hu: Egy korábban figyelt aktor figyelésének leállítása.
    /// <br />
    /// en: Stop watching a previously watched actor.
    /// </summary>
    /// <param name="ATarget">
    /// hu: Az aktor referenciája, akinek figyelését le akarjuk állítani.
    /// <br />
    /// en: The reference of the actor to stop watching.
    /// </param>
    void Unwatch(TActorRef ATarget);
}
