# Attachment 2 — Roadmap with hour estimates

> NLnet NGI Zero Commons Fund — Symphact application, 13th open call (deadline 2026-06-01).
> Condensed from `docs/roadmap-en.md` v1.0. Full text: [`roadmap-en.md`](../roadmap-en.md).

## Summary table

| Phase | Milestones | Est. hours | Delivers |
|-------|-----------|------------|----------|
| **Phase 1** Core Runtime | M0.1–M1.0 | ~148–204 | Actor runtime + CFPU reference impl |
| **Phase 2** Kernel Foundation | M2.1–M2.5 | ~120–160 | Scheduler, router, memory, capabilities |
| **Phase 3** Device & I/O | M3.1–M3.5 | ~80–120 | Device actors (UART, GPIO, timer, storage) |
| **Phase 4** Advanced Runtime | M4.1–M4.4 | ~100–140 | Hot code loading, migration, backpressure |
| **Phase 5** Security & Compliance | M5.1–M5.4 | ~100–140 | Capability security, audit, formal verification |
| **Phase 6** Developer Experience | M6.1–M6.5 | ~100–140 | CLI, IDE, profiler, NuGet, docs |
| **Phase 7** First Sellable Product | M7.1–M7.5 | ~80–120 | Reference apps, enterprise, launch |
| **Total** | | **~728–1024** | **Production-ready Symphact** |

> Estimates are calibrated to M0.2 actuals (~8 hours, 580 lines of code, 46 tests, medium complexity).

## Phase 1 — Core Runtime (M0.1–M1.0)

| Milestone | Status | Tests | Est. hours | Highlights |
|-----------|--------|-------|------------|------------|
| M0.1 Actor core primitives | ✅ | 30 | ~6 | `IMailbox`/`TMailbox`, `TActorRef`, `TActor<TState>`, `TActorSystem` |
| M0.2 ActorContext + inter-actor messaging | ✅ | +16 | ~8 (actual) | `IActorContext`/`TActorContext`, `DrainAsync` with `MaxRounds` |
| M0.3 Supervision (let-it-crash) | ✅ | +30 | ~24–32 | `ISupervisorStrategy`, OneForOne/AllForOne, lifecycle hooks |
| M0.4 Scheduler + per-actor parallelism | ✅ | +86 | ~24–32 (actual ~6h agent team) | `IScheduler`, `TInlineScheduler`, `TDedicatedThreadScheduler`, `QuiesceAsync` |
| M0.5 Persistence — BCL-only slices | ✅ | +44 | shipped | `IJournal` + `TInMemoryJournal`, `ISnapshotStore` + `TInMemorySnapshotStore` |
| M0.5 Persistence — content-addressed | 🚧 | — | ~16–24 | `TCasJournal` + `TCasSnapshotStore` (SHA-256), supervision lifecycle integration |
| M0.6 Remoting / Distribution | ⏳ | — | ~28–36 | `ITransport` + `TTcpTransport`, serialization, location-transparent `Send` |
| M0.7 CFPU Hardware Integration | ⏳ | — | ~30–50 | `TMmioMailbox`, end-to-end demo over `FenySoft.CilCpu.Sim` |
| M1.0 Samples + Stabilization | ⏳ | — | ~12–16 | CounterActor, ChatRoom, API reference, benchmarks |

**Total green tests at grant submission: 186** (120 Core + 22 Platform.DotNet + 44 Persistence).

## Phase 2 — Kernel Foundation (M2.1–M2.5, ~120–160h)

| Milestone | Est. hours | Description |
|-----------|------------|-------------|
| M2.1 Root Supervisor + Actor Hierarchy | ~20–28 | `TRootSupervisor`, boot sequence, fixed restart strategy |
| M2.2 Scheduler Actor | ~28–36 | `ISchedulerPolicy`, `TRoundRobinPolicy`, `TLoadBalancePolicy`, actor-to-core affinity, watchdog |
| M2.3 Router Actor | ~24–32 | `TRouter`, location-transparent `Send`, migration support, dead-letter handling |
| M2.4 Memory Manager | ~20–28 | `TMemoryManager`, per-core SRAM budget, per-actor GC, memory pressure notification |
| M2.5 Capability Registry | ~28–36 | `TCapabilityRegistry`, CST HW table management, delegation + revocation, 8-bit permission flag |

## Phase 3 — Device & I/O Layer (M3.1–M3.5, ~80–120h)

| Milestone | Est. hours | Description |
|-----------|------------|-------------|
| M3.1 Device Actor Framework | ~20–28 | `IDeviceActor`, MMIO ownership, interrupt → message conversion |
| M3.2 UART Device Actor | ~12–16 | TX/RX actor pair, subscribe pattern, backpressure |
| M3.3 GPIO Device Actor | ~8–12 | Pin config (Input/Output/Interrupt), edge-triggered IRQ → message |
| M3.4 Timer / Clock Service | ~12–16 | `TTimerService`, `AskWithTimeout`, mock clock for testing |
| M3.5 Storage Service | ~28–36 | `IStorageProvider`, `TFileSystemActor`, file handle = actor |

## Phase 4 — Advanced Runtime (M4.1–M4.4, ~100–140h)

| Milestone | Est. hours | Description |
|-----------|------------|-------------|
| M4.1 Hot Code Loading | ~32–40 | `THotCodeLoader`, opcode whitelist, Erlang-style version switch, state preservation |
| M4.2 Actor Migration | ~24–32 | Suspend → serialize → move; router mapping update; cross-core / cross-node |
| M4.3 Backpressure + Flow Control | ~20–28 | Mailbox depth limit, `TrySend`, backpressure propagation |
| M4.4 Priority Mailbox + System Messages | ~24–32 | `TPriorityMailbox`, opt-in per actor, watchdog kill, graceful shutdown |

## Phase 5 — Security & Compliance (M5.1–M5.4, ~100–140h)

| Milestone | Est. hours | Description |
|-----------|------------|-------------|
| M5.1 Full Capability Model | ~28–36 | CST HW lookup on every Send, audit trail, AuthCode integration |
| M5.2 Capability Delegation & Revocation | ~20–28 | Attenuation on delegation, revocation broadcast |
| M5.3 Audit & Compliance Logging | ~20–28 | Tamper-proof audit log actor, IEC 61508 / ISO 26262 baseline reporting |
| M5.4 Formal Verification Foundations | ~32–48 | TLA+ / Dafny spec of `send`/`receive` semantics, capability invariants |

## Phase 6 — Developer Experience (M6.1–M6.5, ~100–140h)

| Milestone | Est. hours | Description |
|-----------|------------|-------------|
| M6.1 CLI Tooling | ~20–28 | `symphact` CLI: `new`, `build`, `run`, `deploy`, `monitor` |
| M6.2 IDE Integration | ~24–32 | VS Code extension, Rider plugin, live actor tree, message breakpoint |
| M6.3 Profiler & Monitoring Dashboard | ~24–32 | Per-actor msg/sec, latency histogram, Prometheus + Grafana export |
| M6.4 NuGet Packages & API Reference | ~16–24 | `Symphact.Core`, `Symphact.Supervision`, `Symphact.Persistence`, `Symphact.Remoting`, `Symphact.Security`, `Symphact.Devices` |
| M6.5 Documentation & Tutorials | ~16–24 | Getting started, architecture deep-dive, Akka.NET / Orleans migration guide |

## Phase 7 — First Sellable Product (M7.1–M7.5, ~80–120h)

| Milestone | Est. hours | Description |
|-----------|------------|-------------|
| M7.1 Reference Applications | ~24–32 | IoT Gateway, AI Agent Cluster, SNN Demo, Chat Server |
| M7.2 Production Hardening | ~20–28 | 10K+ actor stress test, chaos testing, soak test, performance regression CI |
| M7.3 Enterprise Features | ~16–24 | RBAC, multi-tenant isolation, configuration management, K8s health check |
| M7.4 Licensing & Packaging | ~8–12 | Dual license, Docker image, Helm chart, landing page |
| M7.5 Launch Preparation | ~12–16 | Announcement blog, conference talks, GitHub Sponsors, first partner onboarding |

---

## Grant-funded scope (12 months, €30 000)

The grant funds the **next stretch beyond what has shipped** — the remaining M0.5 work, M0.6, M0.7, plus selected pieces of Phase 5 (formal-verification foundations) and Phase 6 (NuGet, docs, outreach). Hot code loading (M4.1) is **explicitly deferred** to a follow-up grant.

| Grant milestone | Roadmap mapping | Hours | Amount | Timeframe |
|---|---|---|---|---|
| **M1: Persistence** | M0.5 (content-addressed) | ~80h | €2 900 | Months 1-3 |
| **M2: Remoting + Capability Registry** | M0.6 + M2.5 | ~160h | €5 800 | Months 2-7 |
| **M3: Kernel + Device actors** | M2.1, M2.3, M3.1-M3.4 | ~180h | €6 500 | Months 5-10 |
| **M4: CFPU integration demo** | M0.7 (partial) | ~80h | €2 900 | Months 8-11 |
| **M5: Dev experience + docs + outreach** | M6.4 + M6.5 (partial) | ~180h | €6 500 | Continuous |
| **M6: Formal-verification foundations** | M5.4 (partial) | ~80h | €2 900 | Months 9-12 |
| **Engineering hours total** | | **~760h** | **€27 500** | **12 months** |
| **Online outreach + documentation** | (video production, GitHub Pages, doc expansion) | — | €2 500 | Continuous |
| **Requested amount** | | | **€30 000** | |

**Expected `osreq-to-cfpu` issues:** 3-7 concrete HW requirements documented over the 12 months — feedback paths include SHA-256/BLAKE3 hash instruction, CST cache, mailbox FIFO depth, supervision fault-notification primitives, SRAM-to-SRAM DMA.
