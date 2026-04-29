using Symphact.Core;

namespace Symphact.Platform.Cfpu;

/// <summary>
/// hu: CFPU hardver platform bridge — összeköti a Symphact runtime-ot a CLI-CPU golden
/// reference szimulátorral (CilCpu.Sim). Jelenleg csonk — a bridge implementáció a
/// CLI-CPU F4 (multi-core) szimulátorával együtt készül.
/// <br />
/// en: CFPU hardware platform bridge — connects the Symphact runtime to the CLI-CPU golden
/// reference simulator (CilCpu.Sim). Currently a stub — the bridge implementation arrives
/// together with the CLI-CPU F4 (multi-core) simulator.
/// </summary>
public sealed class TCfpuPlatform : IPlatform
{
    /// <inheritdoc />
    public IMailbox CreateMailbox() =>
        throw new NotSupportedException(
            "CFPU platform requires the CLI-CPU simulator (CilCpu.Sim). " +
            "See https://github.com/FenySoft/CLI-CPU for the F4 multi-core milestone.");
}
