# Symphact

> **Capability-based actor runtime for secure .NET computing — co-designed with the Cognitive Fabric Processing Unit (CFPU).**
> Every entity is an actor. Communication happens exclusively through messages. Software-enforced isolation on .NET hosts, hardware-enforced on CFPU. Formal verifiability and co-evolution with open silicon.

> Magyar verzió: [README-hu.md](README-hu.md)

> Version: 0.1 (pre-alpha — active development)

## What is Symphact?

Symphact is a **capability-based actor runtime** for .NET, built on a simple idea:

> Every stateful entity is an actor. Actors communicate exclusively through immutable messages over mailboxes. Isolation is enforced by the runtime on .NET hosts, and as a hardware property on CFPU.

Today, Symphact runs on **any .NET host** (Windows, Linux, macOS) as a reference runtime. Tomorrow, it will run natively on the **Cognitive Fabric Processing Unit (CFPU)** — a new category of processing unit where each core is physically an actor, with private SRAM and hardware mailbox FIFOs.

**The two projects are co-developed on purpose:** the OS shapes the hardware requirements, and the hardware grounds the OS design. This bidirectional loop is how Apple's M-series chips achieve such tight OS/hardware integration — we apply the same philosophy to an open-source stack.

## Why a separate repository?

Symphact has its own repo for three reasons:

1. **Distinct contributor audience** — .NET developers should not need to read Verilog, cocotb, or Yosys scripts to contribute to an actor runtime
2. **Independent lifecycle** — Symphact runs on any CIL host today; it does not block on silicon availability
3. **Clean licensing** — Apache-2.0 (permissive) aligns with the broader .NET ecosystem; the CFPU hardware repo uses CERN-OHL-S (strong reciprocal), appropriate for silicon designs

The hardware co-development story is in [`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU) — the open-source reference implementation of the CFPU.

## Quick Start

```bash
git clone https://github.com/FenySoft/Symphact.git
cd Symphact
dotnet build Symphact.sln -c Debug
dotnet test
```

## Design principles

1. **Everything is an actor.** No exception. Device drivers, supervisors, services, business logic — all actors.
2. **No shared memory, ever.** Cores and actors communicate only via immutable messages through mailboxes.
3. **Let it crash.** An actor that fails is restarted by its supervisor. The system does not defensively handle every error.
4. **Supervision hierarchy.** Every actor has a supervisor. Failures propagate up the tree until handled.
5. **Location transparency.** An actor reference does not reveal whether the target is local, remote, or on a different chip.
6. **Capability-based security.** An actor can send messages to another only if it holds a capability (an unforgeable reference).
7. **Hot code loading.** A running system can accept new code without downtime (Erlang-style).
8. **Determinism by default.** Same inputs, same state. Reproducible bugs, replay debugging, formal verification.

## Project state

**v0.1 — pre-alpha.** Development just started. The first deliverables are:

- [ ] `IMailbox` + `TMailbox` in-memory implementation (~8 tests, TDD)
- [ ] `TActorRef` value type (~5 tests)
- [ ] `TActor<TState>` + `TActorSystem` with `Spawn` / `Send` / `Receive` (~10 tests)
- [ ] First end-to-end demo: `CounterActor`

Full roadmap: [`docs/roadmap.md`](docs/roadmap.md).

## Relationship with CLI-CPU / CFPU

Symphact runs on **any** CIL host. For hardware co-design purposes, we additionally run Symphact workloads against the CLI-CPU reference simulator (via the upcoming `FenySoft.CilCpu.Sim` NuGet package) to discover hardware requirements:

- Mailbox depth profiling → informs CFPU FIFO sizing
- Context size measurement → informs per-core SRAM budget
- Capability token format → informs router hardware width
- Device actor patterns → informs MMIO abstraction on the first chip (F3 Tiny Tapeout)

OS-to-hardware requirements are tracked via the [`osreq-to-cfpu`](.github/ISSUE_TEMPLATE/osreq-for-cfpu.md) issue template and the [`docs/osreq-to-cfpu/`](docs/osreq-to-cfpu/) directory.

## License

Apache License 2.0 — see [LICENSE](LICENSE) and [NOTICE](NOTICE).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

---

## Changelog

| Version | Date | Summary |
|---------|------|---------|
| 0.1 | 2026-04-16 | Initial repo scaffolding. Apache-2.0 license, .NET project structure, first TDD iteration target. |
