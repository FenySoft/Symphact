# Symphact

> **Capability-alapú aktor runtime biztonságos .NET számításhoz — co-designed a Cognitive Fabric Processing Unit (CFPU) hardverrel.**
> Minden entitás aktor. A kommunikáció kizárólag üzenetküldéssel történik. .NET hoszton szoftveres, CFPU-n hardveres izoláció. Formális verifikálhatóság, és közös fejlődés a nyílt szilíciummal.

> English version: [README.md](README.md)

> Verzió: 0.5 (pre-alfa — aktív fejlesztés)

## Mi a Symphact?

A Symphact egy **capability-alapú aktor runtime** .NET-re, egyetlen egyszerű elvre építve:

> Minden állapot-tartó entitás egy aktor. Az aktorok kizárólag immutable üzenetekkel kommunikálnak, mailbox-okon keresztül. Az izolációt .NET hoszton a runtime biztosítja, CFPU-n a **hardver kényszeríti ki**.

Ma a Symphact **bármely .NET hoszton** (Windows, Linux, macOS) fut referencia runtime-ként. Holnap natívan fog futni a **Cognitive Fabric Processing Unit (CFPU)** hardveren — egy új kategóriájú feldolgozó egységen, ahol minden aktor **dedikált core-on fut**, saját privát SRAM-mal és hardveres mailbox FIFO-kkal.

**A két projektet tudatosan együtt fejlesztjük:** az OS alakítja a hardveres követelményeket, a hardver pedig földbe ereszti az OS tervezési döntéseit. A testvér hardver-projekt: [`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU) (CERN-OHL-S-2.0).

## Miért külön repository?

A Symphact három okból kapott önálló repót:

1. **Eltérő fejlesztői közönség** — egy .NET fejlesztőnek ne kelljen Verilog-ot, cocotb-t vagy Yosys scripteket olvasnia ahhoz, hogy aktor runtime-hoz hozzájáruljon
2. **Független életciklus** — a Symphact bármely CIL host-on fut ma; nem blokkol a szilícium elkészültén
3. **Tiszta licensz** — Apache-2.0 (permisszív) illeszkedik a szélesebb .NET ökoszisztémához; a CFPU hardver repo CERN-OHL-S-t használ (strong reciprocal), ami hardver design-okhoz megfelelő

## Gyors indulás

```bash
git clone https://github.com/FenySoft/Symphact.git
cd Symphact
dotnet build Symphact.sln -c Debug
dotnet test
```

A CI Ubuntu / Windows / macOS-en fut a **.NET 10 SDK** ellen. A `Directory.Build.props` beállítja a `TreatWarningsAsErrors=true` és `GenerateDocumentationFile=true` opciókat — minden warning build-törő, és minden public tagnak kell XML doksi.

## Tervezési alapelvek

1. **Minden aktor.** Kivétel nélkül. Device driverek, supervisorok, service-ek, üzleti logika — mind aktor.
2. **Nincs shared memory, soha.** A core-ok és aktorok kizárólag immutable üzenetekkel kommunikálnak mailbox-okon át.
3. **Let it crash.** Egy hibázó aktort a supervisor-a újraindítja. A rendszer nem védekezik minden hibára defenzíven.
4. **Supervision hierarchia.** Minden aktornak van supervisor-a. A hiba a fa mentén fölfelé propagál, amíg valaki lekezeli.
5. **Location transparency.** Egy aktor referencia nem árulja el, hogy a célpont lokális, távoli, vagy másik chip-en van.
6. **Capability-alapú biztonság.** Egy aktor csak akkor küldhet üzenetet egy másiknak, ha birtokolja a capability-t (nem hamisítható referencia).
7. **Hot code loading.** Egy futó rendszer képes új kódot fogadni leállás nélkül (Erlang-stílusban). *(Tervezett, követő pályázatra halasztva.)*
8. **Determinizmus alapértelmezésben (aktoronként).** Minden aktor a saját üzeneteit determinisztikus FIFO sorrendben látja. Ugyanaz a bemenet, ugyanaz az állapot — reprodukálható bugok, replay debuggolás, formális verifikáció.

## Projekt állapot

**v0.5 — pre-alfa, aktív fejlesztés.** Az M0.1 → M0.4 mérföldkövek készen vannak; az M0.5 (perzisztencia) folyamatban — a BCL-only referencia szeletei már leszállítva.

**Összes zöld xUnit teszt: 186** (120 Core + 22 Platform.DotNet + 44 Persistence).

| Mérföldkő | Státusz | Tesztek | Tartalom |
|-----------|---------|---------|----------|
| **M0.1** Aktor mag primitívek | ✅ | 30 | `IMailbox` / `TMailbox`, `TActorRef`, `TActor<TState>`, `TActorSystem` |
| **M0.2** ActorContext + aktor-közi üzenetküldés | ✅ | +16 | `IActorContext` / `TActorContext`, `DrainAsync` `MaxRounds`-szal |
| **M0.3** Supervision (let-it-crash) | ✅ | +30 | `ISupervisorStrategy`, OneForOne / AllForOne, lifecycle hookok, hierarchia |
| **M0.4** Scheduler + per-aktor parallelizmus | ✅ | +86 | `IScheduler`, `TInlineScheduler`, `TDedicatedThreadScheduler` (CFPU dedikált-core szimuláció, 1000 aktor cap), `IMailboxSignal` / `TDotNetMailboxSignal`, `QuiesceAsync` |
| **M0.5** Perzisztencia — BCL-only szeletek | ✅ | +44 | `IJournal` + `TInMemoryJournal`, `ISnapshotStore` + `TInMemorySnapshotStore` |
| **M0.5** Perzisztencia — content-addressed | 🚧 | — | `TCasJournal` + `TCasSnapshotStore` (SHA-256 content-addressed tárolás), supervision lifecycle integráció |
| **M0.6** Remoting + capability registry | ⏳ | — | `ITransport` / `TTcpTransport`, `TCapabilityRegistry` |
| **M0.7** CFPU integrációs demó | ⏳ | — | End-to-end demó a `FenySoft.CilCpu.Sim` felett |

Teljes roadmap: [`docs/roadmap-hu.md`](docs/roadmap-hu.md).

## Architektúra (aktuális scope)

Három primitív alkotja az aktor magot:

1. **`IMailbox` / `TMailbox`** (`src/Symphact.Core/IMailbox.cs`, `TMailbox.cs`) — FIFO mailbox `ConcurrentQueue<object>` felett (lock-free MPMC). Úgy van tervezve, hogy egy jövőbeli `TMmioMailbox` (CFPU hardver FIFO ellen) API-változtatás nélkül cserélhető legyen.
2. **`TActorRef`** (`src/Symphact.Core/TActorRef.cs`) — `readonly record struct TActorRef(int SlotIndex)`, opaque CST (Capability Slot Table) index. A `default` érvénytelen (`TActorRef.Invalid`).
3. **`TActor<TState>` + `TActorSystem`** (`src/Symphact.Core/TActor.cs`, `TActorSystem.cs`) — `TActor<TState>` absztrakt (`Init()`, `Handle(state, msg)`); `TActorSystem` a runtime (`Spawn`, `Send`, `DrainAsync`, `QuiesceAsync`).

Az ütemezés az **`IScheduler`** (`src/Symphact.Core/IScheduler.cs`) interfészen keresztül szétválasztva:

- **`TInlineScheduler`** — szinkron, single-threaded referencia (szigorú globális sorrendet tart).
- **`TDedicatedThreadScheduler`** — egy OS thread per aktor, a CFPU dedikált-core-per-aktor modell szimulálva (alap cap: 1000 aktor, konfigurálható).

A supervision (M0.3) szolgáltatja az `ISupervisorStrategy`-t, az OneForOne / AllForOne stratégiákat, a lifecycle hookokat (`PreStart`, `PostStop`, `PreRestart`, `PostRestart`) és az aktor hierarchiát.

A perzisztencia (M0.5, folyamatban) szolgáltatja az `IJournal` / `ISnapshotStore` interfészeket — a BCL-only in-memory referencia implementációk készen vannak; a content-addressed produkciós szintű változat (`TCasJournal`, `TCasSnapshotStore`) jön következőre.

## Projekt struktúra

```
src/Symphact.Core/              runtime + HAL interfészek (IScheduler, IMailboxSignal, supervision, …)
src/Symphact.Platform.DotNet/   .NET referencia platform (ConcurrentQueue mailbox, AutoResetEvent signal)
src/Symphact.Platform.Cfpu/     CLI-CPU szimulátor bridge (stub — várja a CFPU F4 multi-core-t)
src/Symphact.Persistence/       IJournal, ISnapshotStore + in-memory referencia implek
tests/Symphact.Core.Tests/                  xUnit (120 teszt)
tests/Symphact.Platform.DotNet.Tests/       xUnit (22 teszt)
tests/Symphact.Persistence.Tests/           xUnit (44 teszt)
docs/                            architektúra, roadmap, trust model, NLnet pályázati draft, osreq-to-cfpu/
samples/                         (üres — a CounterActor demó az első cél)
.github/workflows/ci.yml         multi-OS build + test
Directory.Build.props            net10.0, warnings-as-errors, docs on
```

## Kapcsolat a CLI-CPU-val / CFPU-val

A Symphact **bármely** CIL hoszton fut. A hardveres co-design céljából **kiegészítésként** a Symphact workload-okat a CLI-CPU referencia szimulátoron is futtatjuk (a hamarosan érkező `FenySoft.CilCpu.Sim` NuGet csomagon át), hogy felfedezzük a hardveres követelményeket:

- Mailbox mélység profilozás → CFPU FIFO méretezés
- Kontextus-méret mérés → per-core SRAM budget
- Capability token formátum → router HW szélesség (CST modell)
- Device aktor minták → MMIO absztrakció az első chipen (Tiny Tapeout)

Az OS-ből a hardver felé kommunikált követelményeket a [`osreq-to-cfpu`](.github/ISSUE_TEMPLATE/osreq-for-cfpu.md) issue template és a [`docs/osreq-to-cfpu/`](docs/osreq-to-cfpu/) könyvtár követi nyomon (osreq-001 … osreq-006 aktív; az osreq-007 elavult, a CST modell váltotta le).

## Finanszírozás

Az **NLnet NGI Zero Commons Fund** (13. nyílt kör, deadline 2026-06-01) felé €30 000 értékű pályázat lett előkészítve az M0.5 (content-addressed perzisztencia) → M0.7 (CFPU integrációs demó) mérföldkövek, plusz a formális verifikációs alapozás, NuGet publikáció, kétnyelvű dokumentáció és outreach finanszírozására. Lásd: [`docs/nlnet-application-draft-hu.md`](docs/nlnet-application-draft-hu.md).

## Licenc

Apache License 2.0 — lásd [LICENSE](LICENSE) és [NOTICE](NOTICE).

## Közreműködés

Lásd [CONTRIBUTING-hu.md](CONTRIBUTING-hu.md). A projekt **szigorúan TDD** — a `src/Symphact.Core/` alatti runtime változtatások előbb egy bukó tesztet igényelnek a `tests/` alatt.

---

## Changelog

| Verzió | Dátum | Összefoglaló |
|--------|-------|-------------|
| 0.5 | 2026-05-19 | M0.3 supervision, M0.4 scheduler + per-aktor parallelizmus és M0.5 BCL-only perzisztencia szeletek (`IJournal` + `TInMemoryJournal`, `ISnapshotStore` + `TInMemorySnapshotStore`) mind leszállítva. **186 zöld xUnit teszt** (120 Core + 22 Platform.DotNet + 44 Persistence). NLnet pályázati draft elkészült. |
| 0.1 | 2026-04-16 | Kezdeti repo csontváz. Apache-2.0 licensz, .NET projekt struktúra, első TDD iteráció célja. |
