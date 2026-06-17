# Symphact

[![CI](https://github.com/FenySoft/Symphact/actions/workflows/ci.yml/badge.svg)](https://github.com/FenySoft/Symphact/actions/workflows/ci.yml)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![Status](https://img.shields.io/badge/status-v0.5_pre--alpha-orange.svg)](docs/roadmap-en.md)

> **Capability-based actor runtime for secure .NET computing — co-designed with the Cognitive Fabric Processing Unit (CFPU).**
> Every entity is an actor. Communication happens exclusively through messages. Software-enforced isolation on .NET hosts, hardware-enforced on CFPU. Formal verifiability and co-evolution with open silicon.

> Magyar verzió: [README-hu.md](README-hu.md)

> Version: 0.5 (pre-alpha — active development)

## What is Symphact?

Symphact is a **capability-based actor runtime** for .NET, built on a simple idea:

> Every stateful entity is an actor. Actors communicate exclusively through immutable messages over mailboxes. Isolation is enforced by the runtime on .NET hosts, and as a hardware property on CFPU.

Today, Symphact runs on **any .NET host** (Windows, Linux, macOS) as a reference runtime. Tomorrow, it will run natively on the **Cognitive Fabric Processing Unit (CFPU)** — a new category of processing unit where every actor runs on a dedicated core, with private SRAM and hardware mailbox FIFOs.

**The two projects are co-developed on purpose:** the OS shapes the hardware requirements, and the hardware grounds the OS design. The sister hardware project is [`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU) (CERN-OHL-S-2.0).

## Why a separate repository?

Symphact has its own repo for three reasons:

1. **Distinct contributor audience** — .NET developers should not need to read Verilog, cocotb, or Yosys scripts to contribute to an actor runtime
2. **Independent lifecycle** — Symphact runs on any CIL host today; it does not block on silicon availability
3. **Clean licensing** — Apache-2.0 (permissive) aligns with the broader .NET ecosystem; the CFPU hardware repo uses CERN-OHL-S (strong reciprocal), appropriate for silicon designs

## Quick Start

```bash
git clone https://github.com/FenySoft/Symphact.git
cd Symphact
dotnet build Symphact.sln -c Debug
dotnet test

# Run the minimal end-to-end actor demo (CounterActor):
dotnet run --project samples/CounterActor/CounterActor.csproj
```

Expected demo output:

```
Symphact CounterActor sample — sending 5 Increment + 2 Decrement + 1 Query

Query reply received: counter = 3
Final state via GetState<int>: 3
```

See [`samples/CounterActor/README.md`](samples/CounterActor/README.md) for what the demo shows.

CI runs on Ubuntu / Windows / macOS against **.NET 10 SDK**. `Directory.Build.props` sets `TreatWarningsAsErrors=true` and `GenerateDocumentationFile=true` — all warnings are build-breaking and all public members need XML docs.

## Design principles

1. **Everything is an actor.** No exception. Device drivers, supervisors, services, business logic — all actors.
2. **No shared memory, ever.** Cores and actors communicate only via immutable messages through mailboxes.
3. **Let it crash.** An actor that fails is restarted by its supervisor. The system does not defensively handle every error.
4. **Supervision hierarchy.** Every actor has a supervisor. Failures propagate up the tree until handled.
5. **Location transparency.** An actor reference does not reveal whether the target is local, remote, or on a different chip.
6. **Capability-based security.** An actor can send messages to another only if it holds a capability (an unforgeable reference).
7. **Hot code loading.** A running system can accept new code without downtime (Erlang-style). *(Planned, deferred to follow-up grant.)*
8. **Determinism by default (per actor).** Each actor sees its messages in deterministic FIFO order. Same inputs, same state — reproducible bugs, replay debugging, formal verification.

## Project state

**v0.5 — pre-alpha, active development.** Milestones M0.1 → M0.4 are complete; M0.5 (persistence) is in progress — the BCL-only reference slices have shipped.

**Total green xUnit tests: 186** (120 Core + 22 Platform.DotNet + 44 Persistence).

| Milestone | Status | Tests | Highlights |
|-----------|--------|-------|------------|
| **M0.1** Actor core primitives | ✅ | 30 | `IMailbox` / `TMailbox`, `TActorRef`, `TActor<TState>`, `TActorSystem` |
| **M0.2** ActorContext + inter-actor messaging | ✅ | +16 | `IActorContext` / `TActorContext`, `DrainAsync` with `MaxRounds` |
| **M0.3** Supervision (let-it-crash) | ✅ | +30 | `ISupervisorStrategy`, OneForOne / AllForOne, lifecycle hooks, hierarchy |
| **M0.4** Scheduler + per-actor parallelism | ✅ | +86 | `IScheduler`, `TInlineScheduler`, `TDedicatedThreadScheduler` (CFPU dedicated-core simulation, cap 1000 actors), `IMailboxSignal` / `TDotNetMailboxSignal`, `QuiesceAsync` |
| **M0.5** Persistence — BCL-only slices | ✅ | +44 | `IJournal` + `TInMemoryJournal`, `ISnapshotStore` + `TInMemorySnapshotStore` |
| **M0.5** Persistence — content-addressed | 🚧 | — | `TCasJournal` + `TCasSnapshotStore` (SHA-256 content-addressed storage), supervision lifecycle integration |
| **M0.6** Remoting + capability registry | ⏳ | — | `ITransport` / `TTcpTransport`, `TCapabilityRegistry` |
| **M0.7** CFPU integration demo | ⏳ | — | End-to-end demo over `FenySoft.CilCpu.Sim` |

Full roadmap: [`docs/roadmap-en.md`](docs/roadmap-en.md).

## Architecture (current scope)

Three primitives form the actor core:

1. **`IMailbox` / `TMailbox`** (`src/Symphact.Core/IMailbox.cs`, `TMailbox.cs`) — FIFO mailbox on `ConcurrentQueue<object>` (lock-free MPMC). Designed so a future `TMmioMailbox` against CFPU hardware FIFO can drop in without API changes.
2. **`TActorRef`** (`src/Symphact.Core/TActorRef.cs`) — `readonly record struct TActorRef(int SlotIndex)`, opaque CST (Capability Slot Table) index. `default` is invalid (`TActorRef.Invalid`).
3. **`TActor<TState>` + `TActorSystem`** (`src/Symphact.Core/TActor.cs`, `TActorSystem.cs`) — `TActor<TState>` is abstract (`Init()`, `Handle(state, msg)`); `TActorSystem` is the runtime (`Spawn`, `Send`, `DrainAsync`, `QuiesceAsync`).

Scheduling is decoupled via **`IScheduler`** (`src/Symphact.Core/IScheduler.cs`):

- **`TInlineScheduler`** — synchronous, single-threaded reference (preserves strict global ordering).
- **`TDedicatedThreadScheduler`** — one OS thread per actor, simulating the CFPU dedicated-core-per-actor model (default cap 1000 actors, configurable).

Supervision (M0.3) provides `ISupervisorStrategy`, OneForOne / AllForOne strategies, lifecycle hooks (`PreStart`, `PostStop`, `PreRestart`, `PostRestart`), and the actor hierarchy.

Persistence (M0.5, in progress) provides `IJournal` / `ISnapshotStore` — the BCL-only in-memory reference implementations are complete; the content-addressed production-grade variant (`TCasJournal`, `TCasSnapshotStore`) is next.

## Validation in practice

The actor model principles in Symphact are not theoretical — they are proven in industry practice. In [*Agents Are Actors*](https://www.youtube.com/watch?v=zdrg987i3vI) (Akka.NET Community Standup, June 2026), the **Netclaw** case study demonstrates that these foundations are sufficient for real-world AI agent workloads: 800 million tokens of durable agent work, event-sourced memory, supervised child actors, parallel tool execution, and recovery from crashes without re-running completed work. The design patterns in Symphact (immutable messages, supervision hierarchy, journaling, capability-based isolation) enable durable, verifiable systems at scale. This convergence with established practice validates that capability-based actors are the right foundation for both software and hardware co-design.

## Project layout

```
src/Symphact.Core/              runtime + HAL interfaces (IScheduler, IMailboxSignal, supervision, …)
src/Symphact.Platform.DotNet/   .NET reference platform (ConcurrentQueue mailbox, AutoResetEvent signal)
src/Symphact.Platform.Cfpu/     CLI-CPU simulator bridge (stub — awaiting CFPU F4 multi-core)
src/Symphact.Persistence/       IJournal, ISnapshotStore + in-memory reference impls
tests/Symphact.Core.Tests/                  xUnit (120 tests)
tests/Symphact.Platform.DotNet.Tests/       xUnit (22 tests)
tests/Symphact.Persistence.Tests/           xUnit (44 tests)
docs/                            architecture, roadmap, trust model, NLnet application draft, osreq-to-cfpu/
samples/CounterActor/            minimal runnable end-to-end demo (Increment / Decrement / Query via capability token)
.github/workflows/ci.yml         multi-OS build + test
Directory.Build.props            net10.0, warnings-as-errors, docs on
```

## Relationship with CLI-CPU / CFPU

Symphact runs on **any** CIL host. For hardware co-design purposes, we additionally run Symphact workloads against the CLI-CPU reference simulator (via the upcoming `FenySoft.CilCpu.Sim` NuGet package) to discover hardware requirements:

- Mailbox depth profiling → informs CFPU FIFO sizing
- Context size measurement → informs per-core SRAM budget
- Capability token format → informs router hardware width (CST model)
- Device actor patterns → informs MMIO abstraction on the first chip (Tiny Tapeout)

OS-to-hardware requirements are tracked via the [`osreq-to-cfpu`](.github/ISSUE_TEMPLATE/osreq-for-cfpu.md) issue template and the [`docs/osreq-to-cfpu/`](docs/osreq-to-cfpu/) directory (osreq-001 … osreq-006 are active; osreq-007 was superseded by the CST model).

## License

Apache License 2.0 — see [LICENSE](LICENSE) and [NOTICE](NOTICE).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). The project is **strictly TDD** — runtime changes in `src/Symphact.Core/` require a failing test first in `tests/`.

---

## Changelog

| Version | Date | Summary |
|---------|------|---------|
| 0.5 | 2026-05-19 | M0.3 supervision, M0.4 scheduler + per-actor parallelism, and M0.5 BCL-only persistence slices (`IJournal` + `TInMemoryJournal`, `ISnapshotStore` + `TInMemorySnapshotStore`) all delivered. **186 green xUnit tests** (120 Core + 22 Platform.DotNet + 44 Persistence). NLnet grant application drafted. |
| 0.1 | 2026-04-16 | Initial repo scaffolding. Apache-2.0 license, .NET project structure, first TDD iteration target. |
