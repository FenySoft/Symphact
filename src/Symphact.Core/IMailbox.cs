namespace Symphact.Core;

/// <summary>
/// hu: Mailbox absztrakció egy aktor bejövő üzeneteinek tárolására. Az üzenet-feldolgozás
/// FIFO sorrendben történik. Thread-safe: több szálról biztonságosan lehet Post-olni és
/// TryReceive-et hívni egyszerre. A CFPU hardveres mailbox FIFO-jának szoftveres interfésze
/// — az implementáció különbözhet (in-memory, MMIO-backed, network-backed), de a
/// szemantika egységes.
/// <br />
/// en: Mailbox abstraction for storing an actor's incoming messages. Message processing is
/// FIFO-ordered. Thread-safe: Post and TryReceive may be called from multiple threads
/// concurrently. The software interface for the CFPU's hardware mailbox FIFO — implementations
/// may differ (in-memory, MMIO-backed, network-backed), but the semantics are uniform.
/// </summary>
public interface IMailbox
{
    /// <summary>
    /// hu: A mailboxban aktuálisan várakozó üzenetek száma. Atomic olvasás.
    /// <br />
    /// en: Number of messages currently queued in the mailbox. Atomic read.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// hu: Üzenet elhelyezése a mailbox végén. Thread-safe.
    /// <br />
    /// en: Place a message at the tail of the mailbox. Thread-safe.
    /// </summary>
    /// <param name="AMessage">
    /// hu: A hozzáadandó üzenet. Nem lehet null.
    /// <br />
    /// en: Message to enqueue. Must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// hu: Ha az AMessage null.
    /// <br />
    /// en: If AMessage is null.
    /// </exception>
    void Post(object AMessage);

    /// <summary>
    /// hu: Egy üzenet eltávolítása a mailbox elejéről. Nem blokkol. Thread-safe.
    /// <br />
    /// en: Remove a message from the head of the mailbox. Does not block. Thread-safe.
    /// </summary>
    /// <param name="AMessage">
    /// hu: Kimenet: a fogadott üzenet, vagy null ha üres volt a mailbox.
    /// <br />
    /// en: Output: the received message, or null if the mailbox was empty.
    /// </param>
    /// <returns>
    /// hu: true, ha sikerült üzenetet fogadni; false, ha a mailbox üres volt.
    /// <br />
    /// en: true if a message was received; false if the mailbox was empty.
    /// </returns>
    bool TryReceive(out object? AMessage);
}
