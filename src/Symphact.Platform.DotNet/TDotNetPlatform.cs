using Symphact.Core;

namespace Symphact.Platform.DotNet;

/// <summary>
/// hu: .NET referencia platform — gyors, korlátlan mock egységtesztekhez. Nincs hardveres
/// mailbox méretkorlát, SRAM limit, vagy CST capability ellenőrzés. A TActorSystem
/// alapértelmezett platformja, ha nincs explicit IPlatform megadva.
/// <br />
/// en: .NET reference platform — fast, unconstrained mock for unit testing. No hardware
/// mailbox size limit, SRAM limit, or CST capability verification. The default platform
/// for TActorSystem when no explicit IPlatform is provided.
/// </summary>
public sealed class TDotNetPlatform : IPlatform, IMailboxSignalProvider
{
    /// <inheritdoc />
    public IMailbox CreateMailbox() => new TMailbox();

    /// <inheritdoc />
    public IMailboxSignal? CreateSignal(IMailbox AMailbox)
    {
        ArgumentNullException.ThrowIfNull(AMailbox);

        return new TDotNetMailboxSignal();
    }
}
