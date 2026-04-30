namespace Symphact.Core;

/// <summary>
/// hu: Mailbox jelzés-csatorna gyár (M0.4). Egy IPlatform implementáció, ami szupportálja
/// az interrupt-stílusú mailbox waiting-et, ezt is implementálja. Ha egy platform nem
/// implementálja, a scheduler-ek a mailbox.Count poll-fallback-re válthatnak (1ms vagy
/// nagyobb periódussal).
/// <br />
/// CFPU megfelelés: a CFPU platform CST/MMIO infrastruktúrája adja a HW-szinten kezelt
/// signal-t — a TCfpuPlatform későbbi implementációja egy MMIO proxy IMailboxSignal-t
/// fog adni, ahol a NotifyMessageArrived no-op (a HW intézi automatikusan).
/// <br />
/// en: Factory of mailbox signal channels (M0.4). An IPlatform implementation that supports
/// interrupt-style mailbox waiting also implements this interface. If a platform does not
/// implement it, schedulers fall back to mailbox.Count polling (1ms or larger period).
/// <br />
/// CFPU correspondence: the CFPU platform's CST/MMIO infrastructure provides HW-managed
/// signalling — a future TCfpuPlatform implementation will return an MMIO-proxy IMailboxSignal
/// where NotifyMessageArrived is a no-op (HW handles it automatically).
/// </summary>
public interface IMailboxSignalProvider
{
    /// <summary>
    /// hu: Új jelzés-csatorna a megadott mailboxhoz. Visszatérhet null-lal, ha a platform
    /// polling-fallback-et szeretne (pl. csonk implementáció).
    /// <br />
    /// en: A new signal channel for the given mailbox. May return null if the platform
    /// requests polling fallback (e.g. stub implementation).
    /// </summary>
    /// <param name="AMailbox">
    /// hu: A mailbox amihez a signal kapcsolódik. A platform szabadon felhasználhatja
    /// implementációhoz (pl. ha a HW signal a mailbox MMIO címéhez kötött).
    /// <br />
    /// en: The mailbox this signal is associated with. The platform may use it freely in
    /// the implementation (e.g. if HW signalling is bound to the mailbox's MMIO address).
    /// </param>
    /// <returns>
    /// hu: A létrehozott signal, vagy null ha a platform polling-fallback-et kér.
    /// <br />
    /// en: The created signal, or null if the platform requests polling fallback.
    /// </returns>
    IMailboxSignal? CreateSignal(IMailbox AMailbox);
}
