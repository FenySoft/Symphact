# Roadmap

> Actor-runtime development milestones — co-designed with the CFPU hardware.

> Version: 1.0

## Summary

| Phase | Milestones | Est. hours | Delivers |
|-------|-----------|------------|----------|
| **Phase 1** Core Runtime | M0.1-M1.0 | ~148-204 | Actor runtime + CFPU ref impl |
| **Phase 2** Kernel Foundation | M2.1-M2.5 | ~120-160 | Scheduler, router, memory, capabilities |
| **Phase 3** Device & I/O | M3.1-M3.5 | ~80-120 | Device actors (UART, GPIO, timer, storage) |
| **Phase 4** Advanced Runtime | M4.1-M4.4 | ~100-140 | Hot code loading, migration, backpressure |
| **Phase 5** Security | M5.1-M5.4 | ~100-140 | Capability security, audit, formal verification |
| **Phase 6** Developer Experience | M6.1-M6.5 | ~100-140 | CLI, IDE, profiler, NuGet, docs |
| **Phase 7** First Sellable Product | M7.1-M7.5 | ~80-120 | Reference apps, enterprise, launch |
| **Total** | | **~728-1024** | **Production-ready Symphact** |

### Phase 1 details (M0.1-M1.0)

| Milestone | Status | Est. hours | Complexity |
|-----------|--------|------------|------------|
| M0.1 | ✅ | ~6 | Medium |
| M0.2 | ✅ | ~8 (actual) | Medium |
| M0.3 | ✅ | ~24-32 | High |
| M0.4 | ✅ | ~24-32 | High |
| M0.5 | Planned | ~16-24 | Medium-high |
| M0.6 | Planned | ~28-36 | High |
| M0.7 | Planned | ~30-50 | Very high |
| M1.0 | Planned | ~12-16 | Medium |

> Estimates are based on M0.2 actuals (~8 hours, 580 lines, 46 tests, medium complexity).
> Agent team (Architect + Implementer + Devil's Advocate + Test Guardian + HW Liaison) hours are given in human-equivalent effort.

---

## M0.1 — Actor Core Primitives ✅

> Core primitives: mailbox, actor ref, actor, actor system.

| Item | Description |
|---|---|
| `IMailbox` / `TMailbox` | FIFO mailbox (lock-free MPMC, `ConcurrentQueue`). |
| `TActorRef` | Capability token (`readonly record struct`, 32-bit CST index). |
| `TActor<TState>` | Abstract actor: `Init()` + `Handle(state, msg)`. |
| `TActorSystem` | Runtime: `Spawn`, `Send`, `DrainAsync`, `GetState` (test-only). |

**Tests:** 30 xUnit | **Est. hours:** ~6

---

## M0.2 — ActorContext + Inter-Actor Messaging ✅

> Actors can send messages to each other from within handlers.

| Item | Description |
|---|---|
| `IActorContext` | Handler context: `Self` (own ref) + `Send(target, msg)`. |
| `TActorContext` | Readonly struct reference impl — delegates to `TActorSystem.Send`. |
| Handle signature | `Handle(TState, object, IActorContext)` — with context parameter. |
| `DrainAsync` MaxRounds | Infinite-loop guard (default: 1000 rounds). |

**CFPU:** Send path is allocation-free in routing. `IActorContext` is narrow enough for MMIO HW FIFO drop-in. `Spawn` intentionally omitted — M0.3 supervision determines its shape.

**Tests:** 46 xUnit (16 new) — 100% dotCover | **Actual hours:** ~8

---

## M0.3 — Supervision (Let-it-crash)

> Fault handling with supervisor strategies, actor hierarchy.

| Item | Description |
|---|---|
| `ISupervisorStrategy` | Resume, Restart, Stop, Escalate decisions. |
| `TOneForOneStrategy` | Handles only the failed child. |
| `TAllForOneStrategy` | Restarts all children. |
| Actor hierarchy | Parent-child relationship — `Spawn` via context. |
| Lifecycle hooks | `PreStart`, `PostStop`, `PreRestart`, `PostRestart`. |

**CFPU:** Restart = core reset + mailbox flush. Supervisor strategy must not assume restart is cheap — CFPU requires HW core reset.

**Potential HW request:** Core reset mechanism: is there HW support for clearing a core's SRAM + mailbox?

**Est. hours:** ~24-32
> Most complex milestone so far. New concepts: supervisor strategy, actor hierarchy (parent-child),
> lifecycle hooks. Deeply affects TActorSystem (spawn becomes hierarchical, DrainAsync error handling,
> restart logic). IActorContext gains Spawn. ~5-7 new files, ~300-400 lines runtime, ~400-600 lines test.

---

## M0.4 — Scheduler + Per-Actor Parallelism ✅

> Parallel execution of multiple actors (single-host, multi-thread).

| Item | Description |
|---|---|
| `IScheduler` / `ISchedulerHost` | Scheduler abstraction + host callback. |
| `TInlineScheduler` | Synchronous, single-threaded; default scheduler. |
| `TDedicatedThreadScheduler` | One .NET Thread per actor — CFPU dedicated-core-per-actor simulation. Default cap 1000 actors. |
| `IMailboxSignal` / `TDotNetMailboxSignal` | Synchronous Wait/Notify (CFPU WFI-compatible). |
| `TActorSystem.QuiesceAsync` | Deterministic barrier. |

**Scope reduction:** `TRoundRobinScheduler` deferred to M2.2 (within `ISchedulerPolicy` context). M0.4 focuses on CFPU simulation via `TDedicatedThreadScheduler`.

**Synchronous supervision:** The strategy call (Restart/Stop/Escalate) runs on the crashed child's thread. No cross-thread mailbox injection (TFailureEnvelope) — the "Capability = reference" invariant is preserved.

**Determinism refined:** per-actor FIFO yes, global ordering under multi-thread no (CLAUDE.md updated).

**CFPU:** Closest to CFPU reality: every actor runs on a dedicated core with private SRAM and HW mailbox FIFO. `TDedicatedThreadScheduler` simulates CFPU with .NET threads. On CFPU there is no "scheduling" — every actor runs on its own core.

**OSREQ candidate:** OSREQ-008 (per-actor stack sizing), OSREQ-009 (sleep/wake idle metric).

**Potential HW request:** Mailbox interrupt vs polling: how does a core learn about new messages?

**Est. hours:** ~24-32
> DrainAsync fundamentally changes: single-threaded → multi-threaded. Thread safety becomes critical
> everywhere. Concurrency testing is inherently hard (race conditions, deadlocks).
> ~3-4 new files, ~200-300 lines runtime, ~300-500 lines test.

**Tests:** 142 xUnit (86 new for M0.4): TInlineScheduler (16), TActorSystemScheduler (8), TActorSystemConcurrency (3), TDotNetMailboxSignal (12), TDedicatedThreadScheduler (11), TDedicatedThreadIntegration (6), TSchedulerStress (5). | **Actual hours:** ~6 (agent team: Architect + Devil's Advocate + Implementer + Test Guardian)
> Scope reduction and Devil's Advocate STOP-pivot gave upfront plan revision; actual TDD cycles ran in 10 steps.

---

## M0.5 — Persistence (Event Sourcing)

> Actor state survives restarts.

| Item | Description |
|---|---|
| `IPersistenceProvider` | Journal abstraction. |
| `TInMemoryJournal` | Test implementation. |
| `TSqliteJournal` | Reference implementation. |
| Snapshot + Replay | State snapshots and replay. |

**CFPU:** Core SRAM is volatile — lost on power loss. Persistence = DMA write to external DRAM/flash. Journal interface must support async, non-blocking writes.

**Potential HW request:** Per-core DMA engine access: dedicated DMA or central controller?

**Est. hours:** ~16-24
> Possible new project (Symphact.Persistence). External dependency (SQLite). Snapshot + replay logic.
> Integration with M0.3 supervision (recovery after restart). ~4-5 new files, ~300-400 lines runtime, ~400-500 lines test.

---

## M0.6 — Remoting / Distribution

> Actors communicate across network / chips.

| Item | Description |
|---|---|
| `TActorRef` extension | `TActorRef(int SlotIndex)` — 32-bit CST index, opaque token. The capability (perms, actor-id, core-coord) resides in the CST (Capability Slot Table) HW table. |
| Serialization layer | Compatible with HW message format. |
| `ITransport` / `TTcpTransport` | Transport abstraction + TCP reference. |

**CFPU:** Multi-chip topology = distribution at HW level. `TActorRef` format must contain chip-id + core-id + mailbox offset. Fixed-size header + payload.

**Potential HW request:** Inter-chip protocol: what link? Max message size? HW routing (mesh NoC)?

**Est. hours:** ~28-36
> TActorRef format change (location info). Serialization layer (message → bytes → message).
> Network code: connection management, reconnection, error handling. Location-transparent Send routing.
> New project (Symphact.Remoting). ~5-7 new files, ~500-700 lines runtime, ~500-700 lines test.

---

## M0.7 — CFPU Hardware Integration

> Hardware FIFO integration — the OS meets the silicon.

| Item | Description |
|---|---|
| `TMmioMailbox` | MMIO-based HW FIFO mailbox implementation. |
| `TActorRef` final format | `TActorRef(int SlotIndex)` — 32-bit CST index. The SlotIndex is an opaque token; capability data (perms, actor-id, core-coord) resides in the HW-managed CST (Capability Slot Table). Interconnect header v3.0: `dst[24]+dst_actor[8] | src[24]+src_actor[8] | seq[16]+flags[8]+len[8] | reserved[8]+CRC-16[16]+CRC-8[8]`. |
| CLI-CPU synchronization | `osreq-to-cfpu` feedback loop with HW repo. **Note:** osreq-007 is OBSOLETE — superseded by the CST model. |

**Est. hours:** ~30-50
> Most uncertain estimate — depends on CLI-CPU HW readiness. MMIO register access from .NET
> (unsafe code or P/Invoke). Final TActorRef bit layout. Hardware testing requires FPGA or simulator.
> ~3-4 new files, ~200-400 lines runtime. Tests limited (HW-dependent tests need simulator).

---

## M1.0 — Samples + Stabilization

> Usable reference runtime with documentation and benchmarks.

| Item | Description |
|---|---|
| CounterActor sample | Simple counter sample app. |
| ChatRoom sample | Multi-actor sample (message routing). |
| API reference | Full public API documentation. |
| Benchmarks | Performance measurements (msg/sec, latency). |

**Est. hours:** ~12-16
> Sample apps, API documentation generation, performance measurements. No new runtime logic —
> stabilization and demonstration of existing APIs.

---

## Phase 2 — Kernel Foundation (M2.1-M2.5)

> The actor runtime becomes a real "OS" — kernel actors manage system resources.

**Total: ~120-160 hours**

### M2.1 — Root Supervisor + Actor Hierarchy Management

> `TRootSupervisor` at the top of the system — every other actor grows from this tree.

| Item | Description |
|---|---|
| `TRootSupervisor` | Starts at boot, never crashes — the root of the system. |
| Actor tree management | Parent-child lookup, tree traversal. |
| System supervisor strategy | Root always restarts — never escalates upward. |
| Boot sequence | Starts in `root → kernel_sup → app_sup` order. |

**CFPU:** Boot sequence runs on the Rich core — root supervisor is the first actor to start, also on silicon.

**Est. hours:** ~20-28
> Builds on M0.3 supervision. New concepts: system-level root, boot sequence, fixed restart strategy.
> ~3-4 new files, ~200-300 lines runtime, ~300-400 lines test.

---

### M2.2 — Scheduler Actor

> Assigning actors to cores, load balancing.

| Item | Description |
|---|---|
| `ISchedulerPolicy` | Pluggable scheduler policy interface. |
| `TRoundRobinPolicy` | Even distribution in round-robin order. |
| `TLoadBalancePolicy` | Load-based core assignment. |
| Actor-to-core affinity | Actor can request to stay pinned to a core. |
| Watchdog | Actor not yielding within N ms → supervisor notification. |

**CFPU:** Cooperative + watchdog timer HW interrupt. No preemption on CFPU — every actor runs on its dedicated core. Scheduler here is a core-allocation decision, not a context switch.

**Potential HW request:** Watchdog timer: is there HW per-core watchdog that sends an interrupt to the Rich core?

**Est. hours:** ~28-36
> Builds on M0.4 scheduler, but now actor-level decisions. Watchdog integration with supervisor tree.
> ~4-5 new files, ~300-400 lines runtime, ~400-500 lines test.

---

### M2.3 — Router Actor

> Logical actor ref → physical address resolution, location-transparent routing.

| Item | Description |
|---|---|
| `TRouter` actor | Ref resolution and routing cache. |
| Location-transparent Send | Local, inter-core, remote — same Send call. |
| Migration support | Actor changes core → router updates mapping. |
| Dead letter handling | Invalid ref → dead letter actor (not exception). |

**CFPU:** HW router handles intra-chip routing; SW router handles inter-chip. Joining the two layers is a critical interface point.

**Potential HW request:** Inter-chip protocol: what link? Max message size? HW routing (mesh NoC)?

**Est. hours:** ~24-32
> Builds on M0.6 remoting transport. Dead letter actor is a new concept. Migration protocol with router.
> ~4-5 new files, ~300-400 lines runtime, ~400-500 lines test.

---

### M2.4 — Memory Manager

> Per-core memory management + Rich core heap pool.

| Item | Description |
|---|---|
| `TMemoryManager` actor | Heap pool allocation for Rich cores. |
| Per-core SRAM budget | Fixed limit on CFPU, simulated limit on .NET. |
| GC trigger policy | Per-actor GC, no global stop-the-world. |
| Memory pressure notification | Too much memory → supervisor notification. |

**CFPU:** Every core has private 16-256 KB SRAM. No shared heap. Memory manager only handles Rich core heap pool — Nano cores need no allocator.

**Potential HW request:** SRAM size per core type: Nano / Rich / GPU core — final figures?

**Est. hours:** ~20-28
> New area: memory management in actor model. Simulated budget limit on .NET.
> ~3-4 new files, ~200-300 lines runtime, ~300-400 lines test.

---

### M2.5 — Capability Registry

> Actor ref as capability token: issuance, delegation, revocation.

| Item | Description |
|---|---|
| `TCapabilityRegistry` actor | Capability ledger and signing authority. |
| `TActorRef` ≡ `TCapability` | The ref is an opaque 32-bit CST index (`TActorRef(int SlotIndex)`). Capability data (perms, actor-id, core-coord) resides in the HW-managed CST — NOT in the token. |
| Spawn issuance | Registry allocates and populates a CST slot at Spawn time. |
| Delegation | Actor can forward the SlotIndex in a message (along with CST-stored perms — attenuation possible). |
| Revocation | CST slot invalidation (event-driven). |
| Permission bit-flag | Send, Stop, Watch, Delegate, Revoke, Query, Snapshot, Migrate — 8 bits. |
| AuthCode integration | Spawn-time signer-blacklist + bytecode SHA blacklist check. |

**CFPU:** The target-core mailbox-edge HW unit performs a CST HW lookup on every Send (NOT the interconnect router — "one mailbox IRQ per core" principle). Invalid CST slot → drop + fail-stop on sender core + AuthCode quarantine against the signer. Details: [trust-model-en.md](trust-model-en.md). (**Note:** osreq-007 is OBSOLETE — superseded by the CST model.)

**Est. hours:** ~28-36
> Builds on M0.6 TActorRef extension. CST HW table management. Capability lifecycle management.
> ~5-6 new files, ~400-500 lines runtime, ~500-600 lines test.

---

## Phase 3 — Device & I/O Layer (M3.1-M3.5)

> Every HW peripheral is an actor — the application accesses it via capability.

**Total: ~80-120 hours**

### M3.1 — Device Actor Framework

> Common base for all device actors: MMIO ownership, interrupt handling, capability-protected access.

| Item | Description |
|---|---|
| `IDeviceActor` | MMIO region ownership + interrupt handling interface. |
| `TDeviceActorBase<TState>` | Abstract base for device actors. |
| MMIO abstraction | `Read(address)` / `Write(address, value)` — simulated on .NET, real on CFPU. |
| Interrupt → message | HW interrupt → mailbox message conversion. |
| Device capability | Only holder of ref has access. |

**CFPU:** Every device actor runs on a Rich core and owns an MMIO region. Nano cores get no device capability — they access peripherals only through the OS.

**Est. hours:** ~20-28
> Possible new project (Symphact.Devices). MMIO simulation layer for .NET.
> ~4-5 new files, ~250-350 lines runtime, ~300-400 lines test.

---

### M3.2 — UART Device Actor

> Serial communication — TX/RX actor pair, subscribe pattern, backpressure.

| Item | Description |
|---|---|
| TX / RX actor pair | Send and receive in separate actors. |
| Subscribe pattern | Application subscribes to RX bytes. |
| Backpressure | TX buffer full → sender blocks. |

**Est. hours:** ~12-16
> Builds on M3.1 Device Actor Framework. UART protocol is known; the challenge is actor model integration.
> ~2-3 new files, ~150-200 lines runtime, ~200-300 lines test.

---

### M3.3 — GPIO Device Actor

> General-purpose I/O — pin configuration, edge-triggered interrupt → message.

| Item | Description |
|---|---|
| Pin config | Input / Output / Interrupt modes. |
| Edge-triggered interrupt | Rising/falling edge → mailbox message. |

**Est. hours:** ~8-12
> Simplest device actor — good introduction for testing the M3.1 framework.
> ~2 new files, ~100-150 lines runtime, ~150-200 lines test.

---

### M3.4 — Timer / Clock Service

> Scheduled messages, timeout pattern, deterministic time for testing.

| Item | Description |
|---|---|
| `TTimerService` actor | Tick messages with configurable interval. |
| Timeout pattern | `AskWithTimeout` — signals error if no reply within N ms. |
| Mock clock | Deterministic time for testing — no real-time waiting. |

**CFPU:** HW timer per core — every core has its own tick generator. TimerService manages the Rich core HW timer.

**Est. hours:** ~12-16
> Mock clock is critical for testability. `AskWithTimeout` integrates with supervision tree.
> ~3-4 new files, ~150-200 lines runtime, ~200-300 lines test.

---

### M3.5 — Storage Service

> Actor-based filesystem — not POSIX byte-stream, but message exchange.

| Item | Description |
|---|---|
| `IStorageProvider` | Storage abstraction interface. |
| `TFileSystemActor` | Structured actor-based storage. |
| File handle = actor | `open` = Spawn, `close` = Stop — the handle is the actor itself. |
| Flash device actor | QSPI flash driver actor. |

**CFPU:** Not POSIX: `Send(fsActor, new ReadMsg(path))` — not `open()`/`read()`. File handle actor has its own capability — only the opener receives it.

**Est. hours:** ~28-36
> Most complex device milestone. Possible new project (Symphact.Storage). Flash device actor is CFPU-specific.
> ~5-7 new files, ~400-500 lines runtime, ~500-600 lines test.

---

## Phase 4 — Advanced Runtime (M4.1-M4.4)

> The "real OS" features that differentiate from competitors.

**Total: ~100-140 hours**

### M4.1 — Hot Code Loading

> Zero-downtime code update — the actor receives a new version while running.

| Item | Description |
|---|---|
| `THotCodeLoader` actor | CIL binary reception, verification, loading. |
| Opcode whitelist | Security check before loading. |
| Version switch | Erlang-style: v1 last message → v2 first message, no interruption. |
| State preservation | State is preserved across version upgrade. |

**CFPU:** Writable microcode SRAM (F6+) — new opcode semantics at runtime. This is the killer feature: zero-downtime update that Akka.NET and Orleans cannot do.

**Est. hours:** ~32-40
> Most uncertain and most exciting milestone. CIL parsing, sandbox verification, Erlang-style hot swap.
> ~5-6 new files, ~400-600 lines runtime, ~500-700 lines test.

---

### M4.2 — Actor Migration

> Actor relocation across cores / nodes — ref stays the same.

| Item | Description |
|---|---|
| Suspend → serialize → move | Actor suspend, state serialization, relocation. |
| Router mapping update | Ref stays the same; router updates the target address. |
| Cross-core migration | Relocation within .NET thread pool. |
| Cross-node migration | Relocation via remoting transport. |
| Load balancing trigger | Scheduler decides when to migrate. |

**CFPU:** Nano → Rich core migration via trap. Migration on CFPU is HW-assisted: core reset + SRAM DMA.

**Est. hours:** ~24-32
> Builds on M2.3 Router Actor and M0.6 Remoting. State serialize/deserialize protocol is critical.
> ~4-5 new files, ~300-400 lines runtime, ~400-500 lines test.

---

### M4.3 — Backpressure + Flow Control

> Mailbox capacity limit, non-blocking Send, backpressure propagation.

| Item | Description |
|---|---|
| Mailbox depth limit | Configurable per-actor capacity. |
| Mailbox full reactions | Sender blocks OR `SendError` trap. |
| `TrySend` | Non-blocking send — returns bool. |
| Backpressure propagation | Pressure propagates upward in supervision tree. |

**CFPU:** HW FIFO depth is fixed (8-64 slots) — natural hardware backpressure. The SW layer must mirror this.

**Potential HW request:** HW FIFO depth per core type: what is the Nano / Rich / GPU mailbox size?

**Est. hours:** ~20-28
> `TMailbox` fundamentally changes — depth limit + block/error strategy. Concurrency testing is hard.
> ~3-4 file modifications + new files, ~200-300 lines runtime, ~300-400 lines test.

---

### M4.4 — Priority Mailbox + System Messages

> System messages preempt application messages — graceful shutdown, watchdog kill.

| Item | Description |
|---|---|
| `TPriorityMailbox` | System messages (shutdown, kill, supervisor) jump the queue. |
| Opt-in per actor | Default remains FIFO — priority is opt-in. |
| Watchdog kill | Watchdog timeout → high-priority kill message. |
| Graceful shutdown | Root → leaf order, with priority messages. |

**Est. hours:** ~24-32
> `IMailbox` extended — priority queue implementation. Graceful shutdown sequence with supervision tree.
> ~3-4 new files, ~200-300 lines runtime, ~300-400 lines test.

---

## Phase 5 — Security & Compliance (M5.1-M5.4)

> Capability-based security — what Akka.NET and Orleans lack, and what sells the product.

**Total: ~100-140 hours**

### M5.1 — Full Capability Model

> Full capability lifecycle: issuance, verification, audit trail.

| Item | Description |
|---|---|
| `TCapability` ≡ `TActorRef` | The capability resides in the CST (Capability Slot Table) HW table; the ref is an opaque 32-bit index (`TActorRef(int SlotIndex)`). NOT a separate struct. |
| CST HW lookup | Target-core mailbox-edge HW unit CST lookup on every Send. |
| Permission bit-flag | Send, Stop, Watch, Delegate, Revoke, Query, Snapshot, Migrate — 8 bits stored in CST. |
| Runtime check | Target-core mailbox-edge HW unit CST lookup on every Send. |
| Audit trail | Invalid CST slot + AuthCode quarantine events logged. |

**CFPU:** The target-core mailbox-edge HW unit performs CST lookup (NOT the interconnect router). Invalid CST slot → drop + fail-stop + AuthCode quarantine. Details: [trust-model-en.md](trust-model-en.md). (**Note:** osreq-007 is OBSOLETE — superseded by the CST model.)

**Est. hours:** ~28-36
> Builds on M2.5 Capability Registry. CST lookup performance is critical — runs on every Send.
> ~4-5 new files, ~300-400 lines runtime, ~400-500 lines test.

---

### M5.2 — Capability Delegation & Revocation

> Fine-grained capability sharing and revocation — with the principle of attenuation.

| Item | Description |
|---|---|
| Delegation | Actor forwards ref in message — receiver gets the capability. |
| Attenuation | Permission can be narrowed on delegation (Write → Read-only). |
| Revocation | Issuer invalidates — all clones become invalid simultaneously. |
| Revocation broadcast | Registry notifies affected routers. |

**Est. hours:** ~20-28
> Builds on M5.1. Revocation broadcast across the full router network — performance-sensitive.
> ~3-4 new files, ~200-300 lines runtime, ~300-400 lines test.

---

### M5.3 — Audit & Compliance Logging

> Tamper-proof audit log for every capability operation — compliance-ready.

| Item | Description |
|---|---|
| Audit logger actor | Every capability operation logged — itself capability-protected. |
| Structured log format | JSON / OpenTelemetry compatible output. |
| Tamper-proof | Nobody can overwrite the log actor — append only. |
| Compliance report | IEC 61508, ISO 26262 baseline reporting. |

**Est. hours:** ~20-28
> Compliance report is critical for the certification path (medical, automotive, aviation).
> ~3-4 new files, ~200-300 lines runtime, ~200-300 lines test.

---

### M5.4 — Formal Verification Foundations

> Kernel actor specification in formal model — foundation of the certification path.

| Item | Description |
|---|---|
| TLA+ / Alloy specification | Formal description of kernel actors. |
| Invariant definitions | Capability never forgeable, actor always isolated. |
| Model checking | Applied to root supervisor, scheduler, router core kernel actors. |

**Est. hours:** ~32-48
> Most uncertain estimate — TLA+ / Alloy learning curve. Not a full formal proof (seL4 level), but foundations laid.

---

## Phase 6 — Developer Experience (M6.1-M6.5)

> Nobody buys a framework that isn't pleasant to use.

**Total: ~100-140 hours**

### M6.1 — CLI Tooling

> `symphact` command-line tool — project templates, build, run, deploy, monitor.

| Item | Description |
|---|---|
| `symphact` CLI | `new`, `build`, `run`, `deploy`, `monitor` commands. |
| `dotnet new symphact-app` | Console actor app template. |
| `dotnet new symphact-service` | Supervised service template. |
| `dotnet new symphact-device` | Device actor template. |

**Est. hours:** ~20-28
> New repo or tool project. dotnet template engine integration.
> ~4-5 new files, ~300-400 lines CLI code, ~200-300 lines test.

---

### M6.2 — IDE Integration

> Visual Studio Code extension + Rider plugin — actor tree visualization, message trace.

| Item | Description |
|---|---|
| VS Code extension | Actor state inspector, message trace viewer. |
| Rider plugin | JetBrains Rider integration. |
| Live actor tree | Real-time supervision hierarchy visualization. |
| Message breakpoint | Breakpoint can be set on message receive (not just line of code). |

**Est. hours:** ~24-32
> Steepest learning curve — VS Code extension API, JetBrains Platform SDK. Two separate plugin projects.

---

### M6.3 — Profiler & Monitoring Dashboard

> Actor performance measurement, system dashboard, Prometheus + Grafana export.

| Item | Description |
|---|---|
| Actor profiler | msg/sec per actor, latency histogram, mailbox depth. |
| System dashboard | Web UI: actor tree, message flow, resource usage. |
| Prometheus export | Standard metrics endpoint. |
| Grafana template | Pre-configured dashboard template. |

**Est. hours:** ~24-32
> Web UI is the most labor-intensive part. Prometheus client library integration is straightforward.
> ~5-6 new files / components, ~400-500 lines code.

---

### M6.4 — NuGet Packages & API Reference

> Publishable NuGet packages and full API documentation.

| Package | Content |
|---------|---------|
| `Symphact.Core` | Runtime primitives. |
| `Symphact.Supervision` | Supervisor strategies. |
| `Symphact.Persistence` | Event sourcing. |
| `Symphact.Remoting` | Distribution. |
| `Symphact.Security` | Capability model. |
| `Symphact.Devices` | Device actor framework. |

API reference site (generated with DocFX or similar).

**Est. hours:** ~16-24
> NuGet packaging is relatively straightforward — packages are already existing projects. API site generation is automated.

---

### M6.5 — Documentation & Tutorials

> Developer documentation and tutorial series — onboarding in 30 minutes.

| Item | Description |
|---|---|
| Getting started | First actor app in 30 minutes. |
| Architecture deep-dive | Developer version of the vision doc. |
| Migration guide | Migration from Akka.NET / Orleans. |
| Tutorial series | IoT gateway, AI agent cluster, SNN demo. |
| FAQ + troubleshooting | Common issues and solutions. |

**Est. hours:** ~16-24
> Documentation writing is time-consuming but not technically difficult. Migration guide is strategically important — it opens the Akka.NET / Orleans market.

---

## Phase 7 — First Sellable Product (M7.1-M7.5)

> The product a company would nod at: "I'll buy this / I'll pay for support."

**Total: ~80-120 hours**

### M7.1 — Reference Applications

> Four reference applications — full demonstration of the framework's capabilities.

| Application | Description |
|---|---|
| IoT Gateway | Sensor actors → protocol actors → cloud actor (MQTT/HTTP). |
| AI Agent Cluster | Supervisor hierarchy + capability-secured LLM agents. |
| SNN Demo | 1000 LIF neuron actors, real-time spike propagation. |
| Chat Server | Multi-room, multi-user, with hot code update demo. |

**Est. hours:** ~24-32
> Every reference app is simultaneously marketing material and integration test. SNN Demo is the direct realization of the CFPU vision.

---

### M7.2 — Production Hardening

> Stress and chaos testing — the system holds up under extreme conditions.

| Item | Description |
|---|---|
| Stress testing | 10K+ actors, sustained load — performance boundary determination. |
| Chaos testing | Random actor kill, network partition simulation. |
| Memory leak detection | Long-running soak test. |
| Performance regression CI | Benchmark on every PR — regression detected automatically. |

**Est. hours:** ~20-28
> Building the chaos testing infrastructure is the most labor-intensive. CI benchmark integration with automatic alerting is critical.

---

### M7.3 — Enterprise Features

> RBAC, multi-tenant isolation, configuration management, K8s health check.

| Item | Description |
|---|---|
| RBAC | Role → capability set mapping. |
| Multi-tenant isolation | Tenant = supervision subtree — strict isolation. |
| Config management | Actor-based configuration service. |
| Health check API | K8s liveness/readiness probe compatible. |

**Est. hours:** ~16-24
> RBAC is a natural extension of the capability model. K8s health check is relatively simple — HTTP endpoint on the actor system.

---

### M7.4 — Licensing & Packaging

> Dual license, Docker image, Helm chart, landing page.

| Item | Description |
|---|---|
| Dual license | Apache-2.0 (community) + Commercial (enterprise support). |
| Docker image | `symphactos-runtime` — production-ready container. |
| Helm chart | K8s deployment template. |
| Landing page | symphact.org — product presentation page. |

**Est. hours:** ~8-12
> Shortest milestone — mostly coordination and marketing tasks. The dual license legal text is the critical element.

---

### M7.5 — Launch Preparation

> Public launch — blog, conference, GitHub Sponsors, first partners.

| Item | Description |
|---|---|
| Announcement blog post | Product and vision presentation. |
| Conference talk | NDC, DotNext, Strange Loop materials. |
| GitHub Sponsors / Open Collective | Community support channel. |
| Enterprise support SLA | Formal support contract document. |
| First partner onboarding | 1-2 early adopter companies. |

**Est. hours:** ~12-16
> Partner onboarding is most important — the first paying customers' feedback shapes Phase 8 (if any).

---

## Changelog

| Version | Date | Summary |
|---------|------|---------|
| 1.0 | 2026-04-18 | Initial release |
