using Symphact.Core;

namespace Symphact.Platform.DotNet;

/// <summary>
/// hu: .NET referencia platform — a .NET hosztokon (Windows, Linux, macOS) futó IPlatform-
/// megvalósítás. Nincs hardveres mailbox méretkorlát, SRAM limit, vagy CST capability
/// ellenőrzés. A TActorSystem minden konstruktora kötelezően IPlatform-ot vár.
/// <br />
/// en: .NET reference platform — the IPlatform implementation running on .NET hosts (Windows,
/// Linux, macOS). No hardware mailbox size limit, SRAM limit, or CST capability verification.
/// Every TActorSystem constructor requires an IPlatform.
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
