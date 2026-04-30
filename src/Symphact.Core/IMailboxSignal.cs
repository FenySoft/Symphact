namespace Symphact.Core;

/// <summary>
/// hu: Mailbox jelzési csatorna (M0.4). Aszimmetrikus szemantikájú interfész: a Wait
/// szinkron blokkol, amíg üzenet nem érkezik (vagy a tokent nem cancel-elik); a Notify
/// jelzi az érkezést. Latching: ha a Notify a Wait előtt fut, a következő Wait azonnal
/// visszatér ("level-triggered"). A jelzés egyszer fogyasztódik.
/// <br />
/// CFPU megfelelés: a Wait egyenértékű a core WFI (Wait For Interrupt) instrukciójával,
/// a NotifyMessageArrived a HW oldalon nem szoftverből hívódik — a mailbox-IRQ vonalat
/// a hardver állítja be a Send oldalán automatikusan. .NET-en a TDotNetMailboxSignal
/// AutoResetEvent-tel implementálja.
/// <br />
/// Szándékos szinkron API: nincs Task allokáció, így a hot path zero-GC. Multi-thread
/// scheduler (pl. TDedicatedThreadScheduler) per-actor thread-ben blokkol egy Wait-en.
/// <br />
/// en: Mailbox signalling channel (M0.4). Asymmetric interface: Wait blocks synchronously
/// until a message arrives (or the token is cancelled); Notify announces arrival. Latching
/// semantics: if Notify runs before Wait, the next Wait returns immediately ("level-triggered").
/// The signal is consumed once.
/// <br />
/// CFPU correspondence: Wait corresponds to the core WFI (Wait For Interrupt) instruction;
/// NotifyMessageArrived is not invoked from software on HW — the mailbox-IRQ line is asserted
/// automatically by hardware on Send. On .NET, TDotNetMailboxSignal implements it via
/// AutoResetEvent.
/// <br />
/// Deliberately synchronous API: no Task allocation, hot path is zero-GC. A multi-threaded
/// scheduler (e.g. TDedicatedThreadScheduler) blocks per-actor on Wait.
/// </summary>
public interface IMailboxSignal : IDisposable
{
    /// <summary>
    /// hu: Szinkron blokkolás új üzenet érkezéséig vagy a token cancellálásáig.
    /// Latching: ha a Notify a Wait előtt fut, azonnal visszatér.
    /// <br />
    /// en: Block synchronously until a new message arrives or the token is cancelled.
    /// Latching: if Notify runs before Wait, returns immediately.
    /// </summary>
    /// <param name="ACancellationToken">
    /// hu: Megszakítási token. Cancel esetén OperationCanceledException.
    /// <br />
    /// en: Cancellation token. On cancellation throws OperationCanceledException.
    /// </param>
    /// <exception cref="OperationCanceledException">
    /// hu: Ha a token jelez cancel-t.
    /// <br />
    /// en: If the token signals cancellation.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// hu: Ha a signal le lett zárva.
    /// <br />
    /// en: If the signal has been disposed.
    /// </exception>
    void Wait(CancellationToken ACancellationToken);

    /// <summary>
    /// hu: Üzenet érkezésének jelzése. Idempotens — több hívás között csak egy Wait
    /// fogyaszt egy jelzést. Dispose után no-op (nem dob exceptiont).
    /// <br />
    /// en: Announce that a message has arrived. Idempotent — between calls only a single
    /// Wait consumes one signal. No-op after Dispose (does not throw).
    /// </summary>
    void NotifyMessageArrived();
}
