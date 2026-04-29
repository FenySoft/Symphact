namespace Symphact.Core;

/// <summary>
/// hu: Egy CFPU core lehetséges állapotai. Megfelel a CORE_STATUS[n] MMIO regiszter
/// értékeinek (OSREQ-002). A szoftveres runtime is ezt az állapotgépet követi.
/// <br />
/// en: Possible states of a CFPU core. Corresponds to the CORE_STATUS[n] MMIO register
/// values (OSREQ-002). The software runtime follows the same state machine.
/// </summary>
public enum ECoreStatus
{
    /// <summary>
    /// hu: A core alszik — mailbox üres, nincs futó kód. Mailbox IRQ ébreszti.
    /// <br />
    /// en: Core is sleeping — mailbox empty, no running code. Woken by mailbox IRQ.
    /// </summary>
    Sleeping = 0,

    /// <summary>
    /// hu: A core fut — üzenetet dolgoz fel.
    /// <br />
    /// en: Core is running — processing a message.
    /// </summary>
    Running = 1,

    /// <summary>
    /// hu: A core hibás állapotban van — trap történt, supervisor döntésre vár.
    /// <br />
    /// en: Core is in error state — a trap occurred, awaiting supervisor decision.
    /// </summary>
    Error = 2,

    /// <summary>
    /// hu: A core reset állapotban van — SRAM törölve, mailbox ürítve, supervisor
    /// döntésre vár az újraindításról.
    /// <br />
    /// en: Core is in reset state — SRAM cleared, mailbox flushed, awaiting supervisor
    /// decision on restart.
    /// </summary>
    Reset = 3
}
