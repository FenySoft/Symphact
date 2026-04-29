namespace Symphact.Core;

/// <summary>
/// hu: Hardver platform absztrakció (HAL). A TActorSystem ezen keresztül éri el a
/// platform-specifikus primitíveket (mailbox, core management). Három implementáció:
/// DotNet (gyors mock), Cfpu (CLI-CPU szimulátor bridge), és a jövőben valódi MMIO.
/// <br />
/// en: Hardware platform abstraction (HAL). TActorSystem accesses platform-specific
/// primitives (mailbox, core management) through this interface. Three implementations:
/// DotNet (fast mock), Cfpu (CLI-CPU simulator bridge), and real MMIO in the future.
/// </summary>
public interface IPlatform
{
    /// <summary>
    /// hu: Új mailbox létrehozása egy aktor számára. A visszaadott implementáció
    /// platform-függő: DotNet-en ConcurrentQueue, CFPU-n hardveres FIFO.
    /// <br />
    /// en: Create a new mailbox for an actor. The returned implementation is
    /// platform-dependent: ConcurrentQueue on DotNet, hardware FIFO on CFPU.
    /// </summary>
    /// <returns>
    /// hu: Az új mailbox példány.
    /// <br />
    /// en: The new mailbox instance.
    /// </returns>
    IMailbox CreateMailbox();
}
