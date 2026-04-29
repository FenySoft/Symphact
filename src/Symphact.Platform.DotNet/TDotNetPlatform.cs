using Symphact.Core;

namespace Symphact.Platform.DotNet;

/// <summary>
/// hu: .NET referencia platform — gyors, korlátlan mock egységtesztekhez. Nincs hardveres
/// mailbox méretkorlát, SRAM limit, vagy HMAC ellenőrzés. A TActorSystem alapértelmezett
/// platformja, ha nincs explicit IPlatform megadva.
/// <br />
/// en: .NET reference platform — fast, unconstrained mock for unit testing. No hardware
/// mailbox size limit, SRAM limit, or HMAC verification. The default platform for
/// TActorSystem when no explicit IPlatform is provided.
/// </summary>
public sealed class TDotNetPlatform : IPlatform
{
    /// <inheritdoc />
    public IMailbox CreateMailbox() => new TMailbox();
}
