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
}
