# Roadmap

> hu: Az aktor-runtime fejlesztési mérföldkövei — a CFPU hardverrel együtt-tervezve.
>
> en: Actor-runtime development milestones — co-designed with the CFPU hardware.

> Version: 1.0

## Összesítés / Summary

| Fázis / Phase | Milestone-ok | Becsült óra / Est. hours | Mit ad? / Delivers |
|---------------|-------------|--------------------------|-------------------|
| **Phase 1** Core Runtime | M0.1-M1.0 | ~148-204 | Actor runtime + CFPU ref impl |
| **Phase 2** Kernel Foundation | M2.1-M2.5 | ~120-160 | Scheduler, router, memory, capabilities |
| **Phase 3** Device & I/O | M3.1-M3.5 | ~80-120 | Device actors (UART, GPIO, timer, storage) |
| **Phase 4** Advanced Runtime | M4.1-M4.4 | ~100-140 | Hot code loading, migration, backpressure |
| **Phase 5** Security | M5.1-M5.4 | ~100-140 | Capability security, audit, formal verification |
| **Phase 6** Developer Experience | M6.1-M6.5 | ~100-140 | CLI, IDE, profiler, NuGet, docs |
| **Phase 7** First Product | M7.1-M7.5 | ~80-120 | Reference apps, enterprise, launch |
| **Összesen / Total** | | **~728-1024** | **Production-ready Symphact** |

### Phase 1 részletezés / Phase 1 details

| Milestone | Állapot / Status | Becsült óra / Est. hours | Komplexitás / Complexity |
|-----------|-----------------|--------------------------|--------------------------|
| M0.1 | ✅ | ~6 | Közepes / Medium |
| M0.2 | ✅ | ~8 (tény / actual) | Közepes / Medium |
| M0.3 | Tervezett / Planned | ~24-32 | Magas / High |
| M0.4 | Tervezett / Planned | ~24-32 | Magas / High |
| M0.5 | Tervezett / Planned | ~16-24 | Közepes-magas / Medium-high |
| M0.6 | Tervezett / Planned | ~28-36 | Magas / High |
| M0.7 | Tervezett / Planned | ~30-50 | Nagyon magas / Very high |
| M1.0 | Tervezett / Planned | ~12-16 | Közepes / Medium |

> hu: A becslések az M0.2 tényadataira épülnek (~8 óra, 580 sor, 46 teszt, közepes komplexitás).
> Az ügynök csapat (Architect + Implementer + Devil's Advocate + Test Guardian + HW Liaison) munkaóráit emberi egyenértékben adjuk meg.
>
> en: Estimates are based on M0.2 actuals (~8 hours, 580 lines, 46 tests, medium complexity).
> Agent team (Architect + Implementer + Devil's Advocate + Test Guardian + HW Liaison) hours are given in human-equivalent effort.

---

## M0.1 — Actor Core Primitives ✅

> hu: Alapprimitívek: mailbox, actor ref, actor, actor system.
>
> en: Core primitives: mailbox, actor ref, actor, actor system.

| Elem / Item | Leírás / Description |
|---|---|
| `IMailbox` / `TMailbox` | hu: FIFO mailbox (lock-free MPMC, `ConcurrentQueue`). / en: FIFO mailbox (lock-free MPMC, `ConcurrentQueue`). |
| `TActorRef` | hu: Capability token (`readonly record struct`, 32-bit CST index). / en: Capability token (`readonly record struct`, 32-bit CST index). |
| `TActor<TState>` | hu: Absztrakt aktor: `Init()` + `Handle(state, msg)`. / en: Abstract actor: `Init()` + `Handle(state, msg)`. |
| `TActorSystem` | hu: Runtime: `Spawn`, `Send`, `DrainAsync`, `GetState` (teszt). / en: Runtime: `Spawn`, `Send`, `DrainAsync`, `GetState` (test-only). |

**Tesztek / Tests:** 30 xUnit | **Becsült óra / Est. hours:** ~6

---

## M0.2 — ActorContext + Inter-Actor Messaging ✅

> hu: Aktorok üzeneteket küldhetnek egymásnak a handler-ből.
>
> en: Actors can send messages to each other from within handlers.

| Elem / Item | Leírás / Description |
|---|---|
| `IActorContext` | hu: Handler kontextus: `Self` (saját ref) + `Send(target, msg)`. / en: Handler context: `Self` (own ref) + `Send(target, msg)`. |
| `TActorContext` | hu: Readonly struct referencia impl — delegál `TActorSystem.Send`-re. / en: Readonly struct reference impl — delegates to `TActorSystem.Send`. |
| Handle signature | hu: `Handle(TState, object, IActorContext)` — context paraméterrel. / en: `Handle(TState, object, IActorContext)` — with context parameter. |
| `DrainAsync` MaxRounds | hu: Végtelen-loop védelem (default: 1000 kör). / en: Infinite-loop guard (default: 1000 rounds). |

**CFPU:**
> hu: A Send path allokáció-mentes a routing-ban. Az `IActorContext` elég szűk MMIO HW FIFO mögé is. `Spawn` szándékosan kimaradt — az M0.3 supervision határozza meg.
>
> en: Send path is allocation-free in routing. `IActorContext` is narrow enough for MMIO HW FIFO drop-in. `Spawn` intentionally omitted — M0.3 supervision determines its shape.

**Tesztek / Tests:** 46 xUnit (16 új / 16 new) — 100% dotCover | **Tény óra / Actual hours:** ~8

---

## M0.3 — Supervision (Let-it-crash)

> hu: Hibakezelés supervisor stratégiákkal, actor hierarchia.
>
> en: Fault handling with supervisor strategies, actor hierarchy.

| Elem / Item | Leírás / Description |
|---|---|
| `ISupervisorStrategy` | hu: Resume, Restart, Stop, Escalate döntések. / en: Resume, Restart, Stop, Escalate decisions. |
| `TOneForOneStrategy` | hu: Csak a hibás child-ot kezeli. / en: Handles only the failed child. |
| `TAllForOneStrategy` | hu: Minden child-ot újraindítja. / en: Restarts all children. |
| Actor hierarchia | hu: Parent-child viszony — `Spawn` a context-en keresztül. / en: Parent-child relationship — `Spawn` via context. |
| Lifecycle hook-ok | hu: `PreStart`, `PostStop`, `PreRestart`, `PostRestart`. / en: `PreStart`, `PostStop`, `PreRestart`, `PostRestart`. |

**CFPU:**
> hu: A restart = core reset + mailbox flush. A supervisor strategy-nek nem szabad feltételezni, hogy a restart olcsó — CFPU-n HW core reset kell.
>
> en: Restart = core reset + mailbox flush. Supervisor strategy must not assume restart is cheap — CFPU requires HW core reset.

**Lehetséges HW kérés / Potential HW request:**
> hu: Core reset mechanizmus: van-e HW support egy core SRAM + mailbox tisztítására?
>
> en: Core reset mechanism: is there HW support for clearing a core's SRAM + mailbox?

**Becsült óra / Est. hours:** ~24-32
> hu: A legösszetettebb milestone eddig. Új fogalmak: supervisor strategy, actor hierarchia (parent-child),
> lifecycle hook-ok. Mélyen érinti a TActorSystem-et (spawn hierarchikussá válik, DrainAsync error handling,
> restart logika). IActorContext bővül Spawn-nal. ~5-7 új fájl, ~300-400 sor runtime, ~400-600 sor teszt.
>
> en: Most complex milestone so far. New concepts: supervisor strategy, actor hierarchy (parent-child),
> lifecycle hooks. Deeply affects TActorSystem (spawn becomes hierarchical, DrainAsync error handling,
> restart logic). IActorContext gains Spawn. ~5-7 new files, ~300-400 lines runtime, ~400-600 lines test.

---

## M0.4 — Scheduler + Per-Actor Parallelism

> hu: Több actor párhuzamos futtatása (single-host, multi-thread).
>
> en: Parallel execution of multiple actors (single-host, multi-thread).

| Elem / Item | Leírás / Description |
|---|---|
| `IScheduler` | hu: Ütemező absztrakció. / en: Scheduler abstraction. |
| `TRoundRobinScheduler` | hu: Referencia implementáció. / en: Reference implementation. |
| `TDedicatedThreadScheduler` | hu: Per-actor thread — CFPU core szimuláció. / en: Per-actor thread — CFPU core simulation. |

**CFPU:**
> hu: Ez a legközelebb a CFPU valóságához: minden core fizikailag egy actor, saját SRAM-mal és HW mailbox FIFO-val. A `TDedicatedThreadScheduler` a CFPU-t szimulálja .NET thread-ekkel. CFPU-n nincs "scheduling" — minden core mindig fut.
>
> en: Closest to CFPU reality: every core is physically an actor with private SRAM and HW mailbox FIFO. `TDedicatedThreadScheduler` simulates CFPU with .NET threads. On CFPU there is no "scheduling" — every core always runs.

**Lehetséges HW kérés / Potential HW request:**
> hu: Mailbox interrupt vs polling: a core hogyan értesül új üzenetről?
>
> en: Mailbox interrupt vs polling: how does a core learn about new messages?

**Becsült óra / Est. hours:** ~24-32
> hu: A DrainAsync alapvetően megváltozik: single-threaded → multi-threaded. Thread safety mindenhol
> kritikussá válik. Concurrency tesztelés inherensen nehéz (race condition-ök, deadlock-ok).
> ~3-4 új fájl, ~200-300 sor runtime, ~300-500 sor teszt.
>
> en: DrainAsync fundamentally changes: single-threaded → multi-threaded. Thread safety becomes critical
> everywhere. Concurrency testing is inherently hard (race conditions, deadlocks).
> ~3-4 new files, ~200-300 lines runtime, ~300-500 lines test.

---

## M0.5 — Persistence (Event Sourcing)

> hu: Actor state túléli a restart-ot.
>
> en: Actor state survives restarts.

| Elem / Item | Leírás / Description |
|---|---|
| `IPersistenceProvider` | hu: Journal absztrakció. / en: Journal abstraction. |
| `TInMemoryJournal` | hu: Teszt implementáció. / en: Test implementation. |
| `TSqliteJournal` | hu: Referencia implementáció. / en: Reference implementation. |
| Snapshot + Replay | hu: Állapot-pillanatképek és visszajátszás. / en: State snapshots and replay. |

**CFPU:**
> hu: Core SRAM volatile — tápelvesztésnél elveszik. Persistence = DMA kiírás külső DRAM/flash-re. A journal interfésznek aszinkron, nem-blokkoló kiírást kell támogatnia.
>
> en: Core SRAM is volatile — lost on power loss. Persistence = DMA write to external DRAM/flash. Journal interface must support async, non-blocking writes.

**Lehetséges HW kérés / Potential HW request:**
> hu: DMA engine hozzáférés core-onként: saját DMA vagy központi controller?
>
> en: Per-core DMA engine access: dedicated DMA or central controller?

**Becsült óra / Est. hours:** ~16-24
> hu: Új projekt lehetséges (Symphact.Persistence). Külső függőség (SQLite). Snapshot + replay logika.
> Integráció M0.3 supervision-nel (recovery restart után). ~4-5 új fájl, ~300-400 sor runtime, ~400-500 sor teszt.
>
> en: Possible new project (Symphact.Persistence). External dependency (SQLite). Snapshot + replay logic.
> Integration with M0.3 supervision (recovery after restart). ~4-5 new files, ~300-400 lines runtime, ~400-500 lines test.

---

## M0.6 — Remoting / Distribution

> hu: Actor-ok hálózaton / chip-ek között kommunikálnak.
>
> en: Actors communicate across network / chips.

| Elem / Item | Leírás / Description |
|---|---|
| `TActorRef` kiterjesztés | hu: `TActorRef(int SlotIndex)` — 32-bit CST index, opaque token. A capability (perms, actor-id, core-coord) a CST (Capability Slot Table) HW táblában van. / en: `TActorRef(int SlotIndex)` — 32-bit CST index, opaque token. The capability (perms, actor-id, core-coord) resides in the CST (Capability Slot Table) HW table. |
| Serialization layer | hu: HW message formátummal kompatibilis. / en: Compatible with HW message format. |
| `ITransport` / `TTcpTransport` | hu: Transport absztrakció + TCP referencia. / en: Transport abstraction + TCP reference. |

**CFPU:**
> hu: Multi-chip topológia = distribution a HW szinten. A `TActorRef` formátumnak tartalmaznia kell chip-id + core-id + mailbox offset-et. Fix méretű header + payload.
>
> en: Multi-chip topology = distribution at HW level. `TActorRef` format must contain chip-id + core-id + mailbox offset. Fixed-size header + payload.

**Lehetséges HW kérés / Potential HW request:**
> hu: Inter-chip protokoll: milyen link? Max message méret? HW routing (mesh NoC)?
>
> en: Inter-chip protocol: what link? Max message size? HW routing (mesh NoC)?

**Becsült óra / Est. hours:** ~28-36
> hu: TActorRef formátum változás (location info). Serialization layer (message → byte → message).
> Hálózati kód: connection management, reconnection, hibakezelés. Location-transparent Send routing.
> Új projekt (Symphact.Remoting). ~5-7 új fájl, ~500-700 sor runtime, ~500-700 sor teszt.
>
> en: TActorRef format change (location info). Serialization layer (message → bytes → message).
> Network code: connection management, reconnection, error handling. Location-transparent Send routing.
> New project (Symphact.Remoting). ~5-7 new files, ~500-700 lines runtime, ~500-700 lines test.

---

## M0.7 — CFPU Hardware Integration

> hu: Hardver FIFO integráció — az OS találkozik a szilíciummal.
>
> en: Hardware FIFO integration — the OS meets the silicon.

| Elem / Item | Leírás / Description |
|---|---|
| `TMmioMailbox` | hu: MMIO-alapú HW FIFO mailbox implementáció. / en: MMIO-based HW FIFO mailbox implementation. |
| `TActorRef` végleges formátum | hu: `TActorRef(int SlotIndex)` — 32-bit CST index. A SlotIndex opaque token; a capability adatok (perms, actor-id, core-coord) a HW-managed CST (Capability Slot Table) táblában vannak. Interconnect header v3.0: `dst[24]+dst_actor[8] | src[24]+src_actor[8] | seq[16]+flags[8]+len[8] | reserved[8]+CRC-16[16]+CRC-8[8]`. / en: `TActorRef(int SlotIndex)` — 32-bit CST index. The SlotIndex is an opaque token; capability data (perms, actor-id, core-coord) resides in the HW-managed CST (Capability Slot Table). Interconnect header v3.0: `dst[24]+dst_actor[8] | src[24]+src_actor[8] | seq[16]+flags[8]+len[8] | reserved[8]+CRC-16[16]+CRC-8[8]`. |
| CLI-CPU szinkronizáció | hu: `osreq-to-cfpu` feedback loop a HW repo-val. **Megjegyzés:** osreq-007 OBSOLETE — a CST modell váltja fel. / en: `osreq-to-cfpu` feedback loop with HW repo. **Note:** osreq-007 is OBSOLETE — superseded by the CST model. |

**Becsült óra / Est. hours:** ~30-50
> hu: A legbizonytalanabb becslés — függ a CLI-CPU HW készültségétől. MMIO register hozzáférés .NET-ből
> (unsafe kód vagy P/Invoke). TActorRef végleges bit layout. Hardver teszteléshez FPGA vagy szimulátor kell.
> ~3-4 új fájl, ~200-400 sor runtime. Teszt korlátozott (HW-függő tesztek szimulátort igényelnek).
>
> en: Most uncertain estimate — depends on CLI-CPU HW readiness. MMIO register access from .NET
> (unsafe code or P/Invoke). Final TActorRef bit layout. Hardware testing requires FPGA or simulator.
> ~3-4 new files, ~200-400 lines runtime. Tests limited (HW-dependent tests need simulator).

---

## M1.0 — Samples + Stabilization

> hu: Használható referencia runtime, dokumentációval és benchmarkokkal.
>
> en: Usable reference runtime with documentation and benchmarks.

| Elem / Item | Leírás / Description |
|---|---|
| CounterActor sample | hu: Egyszerű számláló minta alkalmazás. / en: Simple counter sample app. |
| ChatRoom sample | hu: Multi-actor minta (üzenetkezelés). / en: Multi-actor sample (message routing). |
| API reference | hu: Teljes publikus API dokumentáció. / en: Full public API documentation. |
| Benchmarks | hu: Teljesítmény mérések (msg/sec, latency). / en: Performance measurements (msg/sec, latency). |

**Becsült óra / Est. hours:** ~12-16
> hu: Minta alkalmazások, API dokumentáció generálás, teljesítmény mérések. Nincs új runtime logika —
> a meglévő API-k stabilizálása és bemutatása.
>
> en: Sample apps, API documentation generation, performance measurements. No new runtime logic —
> stabilization and demonstration of existing APIs.

---

## Phase 2 — Kernel Foundation

> hu: Az aktor runtime-ből valódi "OS" lesz — kernel aktorok kezelik a rendszer erőforrásait.
>
> en: The actor runtime becomes a real "OS" — kernel actors manage system resources.

---

## M2.1 — Root Supervisor + Actor Hierarchy Management

> hu: A rendszer csúcsán álló `TRootSupervisor` — minden más actor ebből a fából nő ki.
>
> en: `TRootSupervisor` at the top of the system — every other actor grows from this tree.

| Elem / Item | Leírás / Description |
|---|---|
| `TRootSupervisor` | hu: Boot-kor indul, soha nem crash-el — a rendszer gyökere. / en: Starts at boot, never crashes — the root of the system. |
| Actor tree management | hu: Parent-child lekérdezés, tree traversal. / en: Parent-child lookup, tree traversal. |
| System supervisor strategy | hu: A root mindig restart-ol — nem eskalál feljebb. / en: Root always restarts — never escalates upward. |
| Boot szekvencia / Boot sequence | hu: `root → kernel_sup → app_sup` sorrendben indul. / en: Starts in `root → kernel_sup → app_sup` order. |

**CFPU:**
> hu: A boot szekvencia a Rich core-on fut — a root supervisor az első actor, ami elindul szilíciumon is.
>
> en: Boot sequence runs on the Rich core — root supervisor is the first actor to start, also on silicon.

**Becsült óra / Est. hours:** ~20-28
> hu: M0.3 supervision-re épül. Új fogalmak: rendszer-szintű gyökér, boot szekvencia, fix restart stratégia.
> ~3-4 új fájl, ~200-300 sor runtime, ~300-400 sor teszt.
>
> en: Builds on M0.3 supervision. New concepts: system-level root, boot sequence, fixed restart strategy.
> ~3-4 new files, ~200-300 lines runtime, ~300-400 lines test.

---

## M2.2 — Scheduler Actor

> hu: Aktorok core-okhoz rendelése, terhelés-elosztás.
>
> en: Assigning actors to cores, load balancing.

| Elem / Item | Leírás / Description |
|---|---|
| `ISchedulerPolicy` | hu: Pluggable ütemező stratégia interfész. / en: Pluggable scheduler policy interface. |
| `TRoundRobinPolicy` | hu: Egyenletes elosztás körbe-körbe. / en: Even distribution in round-robin order. |
| `TLoadBalancePolicy` | hu: Terhelés alapú core-hozzárendelés. / en: Load-based core assignment. |
| Actor-to-core affinity | hu: Az actor kérheti, hogy ragadjon egy core-on. / en: Actor can request to stay pinned to a core. |
| Watchdog | hu: Ha egy actor nem yield-el N ms-en belül → supervisor értesítés. / en: Actor not yielding within N ms → supervisor notification. |

**CFPU:**
> hu: Cooperative + watchdog timer HW interrupt. CFPU-n nincs preemption — minden core mindig az ő actor-ját futtatja. A scheduler itt core-allokációs döntés, nem context switch.
>
> en: Cooperative + watchdog timer HW interrupt. No preemption on CFPU — every core always runs its actor. Scheduler here is a core-allocation decision, not a context switch.

**Lehetséges HW kérés / Potential HW request:**
> hu: Watchdog timer: van-e HW per-core watchdog, ami interrupt-ot küld a Rich core-nak?
>
> en: Watchdog timer: is there HW per-core watchdog that sends an interrupt to the Rich core?

**Becsült óra / Est. hours:** ~28-36
> hu: M0.4 scheduler-re épül, de most actor-szintű döntések. Watchdog integráció a supervisor tree-vel.
> ~4-5 új fájl, ~300-400 sor runtime, ~400-500 sor teszt.
>
> en: Builds on M0.4 scheduler, but now actor-level decisions. Watchdog integration with supervisor tree.
> ~4-5 new files, ~300-400 lines runtime, ~400-500 lines test.

---

## M2.3 — Router Actor

> hu: Logikai actor ref → fizikai cím feloldás, location-transparent routing.
>
> en: Logical actor ref → physical address resolution, location-transparent routing.

| Elem / Item | Leírás / Description |
|---|---|
| `TRouter` actor | hu: Ref resolution és routing cache. / en: Ref resolution and routing cache. |
| Location-transparent Send | hu: Local, inter-core, remote — ugyanaz a Send hívás. / en: Local, inter-core, remote — same Send call. |
| Migration support | hu: Actor core-t vált → router frissíti a mappinget. / en: Actor changes core → router updates mapping. |
| Dead letter handling | hu: Érvénytelen ref → dead letter actor (nem exception). / en: Invalid ref → dead letter actor (not exception). |

**CFPU:**
> hu: A HW router a chip-en belüli routing-ot végzi; a SW router a chip-ek közöttit. A két réteg összeillesztése kritikus interfész pont.
>
> en: HW router handles intra-chip routing; SW router handles inter-chip. Joining the two layers is a critical interface point.

**Lehetséges HW kérés / Potential HW request:**
> hu: Inter-chip protokoll: milyen link? Max message méret? HW routing (mesh NoC)?
>
> en: Inter-chip protocol: what link? Max message size? HW routing (mesh NoC)?

**Becsült óra / Est. hours:** ~24-32
> hu: M0.6 remoting transport-ra épül. Dead letter actor új fogalom. Migration protocol a routerrel.
> ~4-5 új fájl, ~300-400 sor runtime, ~400-500 sor teszt.
>
> en: Builds on M0.6 remoting transport. Dead letter actor is a new concept. Migration protocol with router.
> ~4-5 new files, ~300-400 lines runtime, ~400-500 lines test.

---

## M2.4 — Memory Manager

> hu: Per-core memóriakezelés + Rich core heap pool.
>
> en: Per-core memory management + Rich core heap pool.

| Elem / Item | Leírás / Description |
|---|---|
| `TMemoryManager` actor | hu: Heap pool allokáció Rich core-oknak. / en: Heap pool allocation for Rich cores. |
| Per-core SRAM budget | hu: Fix limit CFPU-n, szimulált limit .NET-en. / en: Fixed limit on CFPU, simulated limit on .NET. |
| GC trigger policy | hu: Per-actor GC, nincs global stop-the-world. / en: Per-actor GC, no global stop-the-world. |
| Memory pressure notification | hu: Túl sok memória → supervisor értesítés. / en: Too much memory → supervisor notification. |

**CFPU:**
> hu: Minden core saját 16-256 KB SRAM. Nincs shared heap. A memory manager csak a Rich core heap pool-t kezeli — a Nano core-ok nem igényelnek allokátort.
>
> en: Every core has private 16-256 KB SRAM. No shared heap. Memory manager only handles Rich core heap pool — Nano cores need no allocator.

**Lehetséges HW kérés / Potential HW request:**
> hu: SRAM méret per core-típusonként: Nano / Rich / GPU core — végleges számok?
>
> en: SRAM size per core type: Nano / Rich / GPU core — final figures?

**Becsült óra / Est. hours:** ~20-28
> hu: Új terület: memória menedzsment actor modelben. .NET-en szimulált budget limit.
> ~3-4 új fájl, ~200-300 sor runtime, ~300-400 sor teszt.
>
> en: New area: memory management in actor model. Simulated budget limit on .NET.
> ~3-4 new files, ~200-300 lines runtime, ~300-400 lines test.

---

## M2.5 — Capability Registry

> hu: Actor ref mint capability token: kiadás, delegálás, visszavonás.
>
> en: Actor ref as capability token: issuance, delegation, revocation.

| Elem / Item | Leírás / Description |
|---|---|
| `TCapabilityRegistry` actor | hu: Capability-ek nyilvántartása és aláírása. / en: Capability ledger and signing authority. |
| `TActorRef` ≡ `TCapability` | hu: A ref egy opaque 32-bit CST index (`TActorRef(int SlotIndex)`). A capability adatok (perms, actor-id, core-coord) a HW-managed CST táblában vannak — NEM a token-ben. / en: The ref is an opaque 32-bit CST index (`TActorRef(int SlotIndex)`). Capability data (perms, actor-id, core-coord) resides in the HW-managed CST — NOT in the token. |
| Spawn-kori kiadás / Spawn issuance | hu: A registry CST slot-ot allokál és tölti ki Spawn-kor. / en: Registry allocates and populates a CST slot at Spawn time. |
| Delegálás / Delegation | hu: Actor üzenetben továbbadhatja a SlotIndex-et (a CST-ben tárolt perms-szel együtt — attenuation lehetséges). / en: Actor can forward the SlotIndex in a message (along with CST-stored perms — attenuation possible). |
| Visszavonás / Revocation | hu: CST slot invalidálás (event-driven). / en: CST slot invalidation (event-driven). |
| Permission bit-flag | hu: Send, Stop, Watch, Delegate, Revoke, Query, Snapshot, Migrate — 8 bit. / en: Send, Stop, Watch, Delegate, Revoke, Query, Snapshot, Migrate — 8 bits. |
| AuthCode integráció / AuthCode integration | hu: Spawn-time aláíró-blacklist + bytecode SHA blacklist check. / en: Spawn-time signer-blacklist + bytecode SHA blacklist check. |

**CFPU:**
> hu: A célcore mailbox-edge HW unit a CST táblában végez HW lookup-ot minden Send-nél (NEM az interconnect router — a "egy mailbox IRQ per core" elv). Érvénytelen CST slot → drop + fail-stop a küldő core-on + AuthCode quarantine az aláíró ellen. Részletek: [trust-model-hu.md](trust-model-hu.md). (**Megjegyzés:** osreq-007 OBSOLETE — a CST modell váltja fel.)
>
> en: The target-core mailbox-edge HW unit performs a CST HW lookup on every Send (NOT the interconnect router — "one mailbox IRQ per core" principle). Invalid CST slot → drop + fail-stop on sender core + AuthCode quarantine against the signer. Details: [trust-model-en.md](trust-model-en.md). (**Note:** osreq-007 is OBSOLETE — superseded by the CST model.)

**Becsült óra / Est. hours:** ~28-36
> hu: M0.6 TActorRef kiterjesztésre épül. CST HW tábla kezelés. Capability lifecycle management.
> ~5-6 új fájl, ~400-500 sor runtime, ~500-600 sor teszt.
>
> en: Builds on M0.6 TActorRef extension. CST HW table management. Capability lifecycle management.
> ~5-6 new files, ~400-500 lines runtime, ~500-600 lines test.

---

## Phase 3 — Device & I/O Layer

> hu: Minden HW periféria actor — az alkalmazás capability-n keresztül éri el.
>
> en: Every HW peripheral is an actor — the application accesses it via capability.

---

## M3.1 — Device Actor Framework

> hu: Közös alap minden device actor-hoz: MMIO tulajdonjog, interrupt kezelés, capability-védett hozzáférés.
>
> en: Common base for all device actors: MMIO ownership, interrupt handling, capability-protected access.

| Elem / Item | Leírás / Description |
|---|---|
| `IDeviceActor` | hu: MMIO régió tulajdonjog + interrupt kezelés interfész. / en: MMIO region ownership + interrupt handling interface. |
| `TDeviceActorBase<TState>` | hu: Absztrakt alap device actor-okhoz. / en: Abstract base for device actors. |
| MMIO absztrakció | hu: `Read(address)` / `Write(address, value)` — .NET-en szimulált, CFPU-n valódi. / en: `Read(address)` / `Write(address, value)` — simulated on .NET, real on CFPU. |
| Interrupt → message | hu: HW interrupt → mailbox message konverzió. / en: HW interrupt → mailbox message conversion. |
| Device capability | hu: Csak az fér hozzá, akinek van ref-je. / en: Only holder of ref has access. |

**CFPU:**
> hu: Minden device actor egy Rich core-on fut, MMIO régiót birtokol. A Nano core-ok nem kapnak device capability-t — csak az OS-en keresztül érnek el perifériát.
>
> en: Every device actor runs on a Rich core and owns an MMIO region. Nano cores get no device capability — they access peripherals only through the OS.

**Becsült óra / Est. hours:** ~20-28
> hu: Új projekt lehetséges (Symphact.Devices). MMIO szimulációs réteg .NET-re.
> ~4-5 új fájl, ~250-350 sor runtime, ~300-400 sor teszt.
>
> en: Possible new project (Symphact.Devices). MMIO simulation layer for .NET.
> ~4-5 new files, ~250-350 lines runtime, ~300-400 lines test.

---

## M3.2 — UART Device Actor

> hu: Soros kommunikáció — TX/RX actor pár, subscribe pattern, backpressure.
>
> en: Serial communication — TX/RX actor pair, subscribe pattern, backpressure.

| Elem / Item | Leírás / Description |
|---|---|
| TX / RX actor pár | hu: Küldés és fogadás szeparált actor-okban. / en: Send and receive in separate actors. |
| Subscribe pattern | hu: Alkalmazás feliratkozik az RX byte-okra. / en: Application subscribes to RX bytes. |
| Backpressure | hu: TX buffer tele → sender blokkolódik. / en: TX buffer full → sender blocks. |

**Becsült óra / Est. hours:** ~12-16
> hu: M3.1 Device Actor Framework-re épül. UART protokoll ismert, a kihívás az actor modell integrálása.
> ~2-3 új fájl, ~150-200 sor runtime, ~200-300 sor teszt.
>
> en: Builds on M3.1 Device Actor Framework. UART protocol is known; the challenge is actor model integration.
> ~2-3 new files, ~150-200 lines runtime, ~200-300 lines test.

---

## M3.3 — GPIO Device Actor

> hu: Általános célú I/O — pin konfigurálás, edge-triggered interrupt → message.
>
> en: General-purpose I/O — pin configuration, edge-triggered interrupt → message.

| Elem / Item | Leírás / Description |
|---|---|
| Pin konfigurálás / Pin config | hu: Input / Output / Interrupt módok. / en: Input / Output / Interrupt modes. |
| Edge-triggered interrupt | hu: Felfutó/lefutó él → mailbox message. / en: Rising/falling edge → mailbox message. |

**Becsült óra / Est. hours:** ~8-12
> hu: A legegyszerűbb device actor — jó bevezetés a M3.1 framework teszteléséhez.
> ~2 új fájl, ~100-150 sor runtime, ~150-200 sor teszt.
>
> en: Simplest device actor — good introduction for testing the M3.1 framework.
> ~2 new files, ~100-150 lines runtime, ~150-200 lines test.

---

## M3.4 — Timer / Clock Service

> hu: Ütemezett üzenetek, timeout pattern, determinisztikus idő teszteléshez.
>
> en: Scheduled messages, timeout pattern, deterministic time for testing.

| Elem / Item | Leírás / Description |
|---|---|
| `TTimerService` actor | hu: Tick üzenetek configurable intervallummal. / en: Tick messages with configurable interval. |
| Timeout pattern | hu: `AskWithTimeout` — ha nem jön válasz N ms-en belül, hibát jelez. / en: `AskWithTimeout` — signals error if no reply within N ms. |
| Mock clock | hu: Determinisztikus idő teszteléshez — nem valódi időt vár. / en: Deterministic time for testing — no real-time waiting. |

**CFPU:**
> hu: HW timer per core — minden core saját tick generátora. A TimerService a Rich core HW timer-ét kezeli.
>
> en: HW timer per core — every core has its own tick generator. TimerService manages the Rich core HW timer.

**Becsült óra / Est. hours:** ~12-16
> hu: Mock clock a tesztelhetőséghez kritikus. Az `AskWithTimeout` a supervision tree-vel integrál.
> ~3-4 új fájl, ~150-200 sor runtime, ~200-300 sor teszt.
>
> en: Mock clock is critical for testability. `AskWithTimeout` integrates with supervision tree.
> ~3-4 new files, ~150-200 lines runtime, ~200-300 lines test.

---

## M3.5 — Storage Service

> hu: Actor-alapú fájlrendszer — nem POSIX byte-stream, hanem üzenetváltás.
>
> en: Actor-based filesystem — not POSIX byte-stream, but message exchange.

| Elem / Item | Leírás / Description |
|---|---|
| `IStorageProvider` | hu: Storage absztrakció interfész. / en: Storage abstraction interface. |
| `TFileSystemActor` | hu: Strukturált actor-alapú storage. / en: Structured actor-based storage. |
| File handle = actor | hu: `open` = Spawn, `close` = Stop — a handle maga az actor. / en: `open` = Spawn, `close` = Stop — the handle is the actor itself. |
| Flash device actor | hu: QSPI flash driver actor. / en: QSPI flash driver actor. |

**CFPU:**
> hu: Nem POSIX: `Send(fsActor, new ReadMsg(path))` — nem `open()`/`read()`. A file handle actor saját capability-vel rendelkezik — csak az kapja meg, aki megnyitotta.
>
> en: Not POSIX: `Send(fsActor, new ReadMsg(path))` — not `open()`/`read()`. File handle actor has its own capability — only the opener receives it.

**Becsült óra / Est. hours:** ~28-36
> hu: Legösszetettebb device milestone. Új projekt lehetséges (Symphact.Storage). Flash device actor CFPU-specifikus.
> ~5-7 új fájl, ~400-500 sor runtime, ~500-600 sor teszt.
>
> en: Most complex device milestone. Possible new project (Symphact.Storage). Flash device actor is CFPU-specific.
> ~5-7 new files, ~400-500 lines runtime, ~500-600 lines test.

---

## Phase 4 — Advanced Runtime

> hu: A "valódi OS" feature-ök, amik a versenytársaktól megkülönböztetnek.
>
> en: The "real OS" features that differentiate from competitors.

---

## M4.1 — Hot Code Loading

> hu: Zero-downtime code frissítés — az actor futás közben kap új verziót.
>
> en: Zero-downtime code update — the actor receives a new version while running.

| Elem / Item | Leírás / Description |
|---|---|
| `THotCodeLoader` actor | hu: CIL binary fogadás, verifikálás, betöltés. / en: CIL binary reception, verification, loading. |
| Opcode whitelist | hu: Biztonsági ellenőrzés betöltés előtt. / en: Security check before loading. |
| Verzióváltás / Version switch | hu: Erlang-style: v1 utolsó üzenet → v2 első üzenet, megszakítás nélkül. / en: Erlang-style: v1 last message → v2 first message, no interruption. |
| State preserváció | hu: Az állapot megmarad version upgrade-nél. / en: State is preserved across version upgrade. |

**CFPU:**
> hu: Writable microcode SRAM (F6+) — új opcode szemantika runtime-ban. Ez a killer feature: zero-downtime update, amit Akka.NET és Orleans nem tud.
>
> en: Writable microcode SRAM (F6+) — new opcode semantics at runtime. This is the killer feature: zero-downtime update that Akka.NET and Orleans cannot do.

**Becsült óra / Est. hours:** ~32-40
> hu: Legbizonytalanabb és legizgalmasabb milestone. CIL parsing, sandbox verifikáció, Erlang-style hot swap.
> ~5-6 új fájl, ~400-600 sor runtime, ~500-700 sor teszt.
>
> en: Most uncertain and most exciting milestone. CIL parsing, sandbox verification, Erlang-style hot swap.
> ~5-6 new files, ~400-600 lines runtime, ~500-700 lines test.

---

## M4.2 — Actor Migration

> hu: Actor áthelyezése core-ok / node-ok között — a ref változatlan marad.
>
> en: Actor relocation across cores / nodes — ref stays the same.

| Elem / Item | Leírás / Description |
|---|---|
| Suspend → serialize → move | hu: Actor felfüggesztés, állapot szerializálás, áthelyezés. / en: Actor suspend, state serialization, relocation. |
| Router mapping frissítés | hu: A ref ugyanaz marad, a router frissíti a célcímet. / en: Ref stays the same; router updates the target address. |
| Cross-core migration | hu: .NET thread pool-on belüli áthelyezés. / en: Relocation within .NET thread pool. |
| Cross-node migration | hu: Remoting transport-on keresztüli áthelyezés. / en: Relocation via remoting transport. |
| Load balancing trigger | hu: A scheduler dönt, mikor migráljunk. / en: Scheduler decides when to migrate. |

**CFPU:**
> hu: Nano → Rich core migration trap-on keresztül. A migráció CFPU-n HW-támogatott: a core reset + SRAM DMA.
>
> en: Nano → Rich core migration via trap. Migration on CFPU is HW-assisted: core reset + SRAM DMA.

**Becsült óra / Est. hours:** ~24-32
> hu: M2.3 Router Actor-ra és M0.6 Remoting-ra épül. State serialize/deserialize protokoll kritikus.
> ~4-5 új fájl, ~300-400 sor runtime, ~400-500 sor teszt.
>
> en: Builds on M2.3 Router Actor and M0.6 Remoting. State serialize/deserialize protocol is critical.
> ~4-5 new files, ~300-400 lines runtime, ~400-500 lines test.

---

## M4.3 — Backpressure + Flow Control

> hu: Mailbox kapacitás limit, nem-blokkoló Send, backpressure propagáció.
>
> en: Mailbox capacity limit, non-blocking Send, backpressure propagation.

| Elem / Item | Leírás / Description |
|---|---|
| Mailbox depth limit | hu: Konfigurálható per-actor kapacitás. / en: Configurable per-actor capacity. |
| Mailbox tele reakciók | hu: Sender blokkolódik VAGY `SendError` trap. / en: Sender blocks OR `SendError` trap. |
| `TrySend` | hu: Non-blocking küldés — bool visszatérési értékkel. / en: Non-blocking send — returns bool. |
| Backpressure propagáció | hu: A supervision tree-n felfelé terjed a nyomás. / en: Pressure propagates upward in supervision tree. |

**CFPU:**
> hu: HW FIFO depth fix (8-64 slot) — természetes hardveres backpressure. A SW rétegnek ezt kell tükröznie.
>
> en: HW FIFO depth is fixed (8-64 slots) — natural hardware backpressure. The SW layer must mirror this.

**Lehetséges HW kérés / Potential HW request:**
> hu: HW FIFO depth per core-típus: mi a Nano / Rich / GPU mailbox mérete?
>
> en: HW FIFO depth per core type: what is the Nano / Rich / GPU mailbox size?

**Becsült óra / Est. hours:** ~20-28
> hu: A `TMailbox` alapvetően módosul — depth limit + block/error stratégia. Concurrency tesztelés nehéz.
> ~3-4 fájl módosítás + új fájlok, ~200-300 sor runtime, ~300-400 sor teszt.
>
> en: `TMailbox` fundamentally changes — depth limit + block/error strategy. Concurrency testing is hard.
> ~3-4 file modifications + new files, ~200-300 lines runtime, ~300-400 lines test.

---

## M4.4 — Priority Mailbox + System Messages

> hu: Rendszerüzenetek megelőzik az alkalmazásüzeneteket — graceful shutdown, watchdog kill.
>
> en: System messages preempt application messages — graceful shutdown, watchdog kill.

| Elem / Item | Leírás / Description |
|---|---|
| `TPriorityMailbox` | hu: System messages (shutdown, kill, supervisor) jump queue. / en: System messages (shutdown, kill, supervisor) jump the queue. |
| Opt-in per actor | hu: Alapértelmezett marad FIFO — prioritás opcionális. / en: Default remains FIFO — priority is opt-in. |
| Watchdog kill | hu: Watchdog timeout → high-priority kill message. / en: Watchdog timeout → high-priority kill message. |
| Graceful shutdown | hu: Root → leaf sorrendben, priority message-ekkel. / en: Root → leaf order, with priority messages. |

**Becsült óra / Est. hours:** ~24-32
> hu: `IMailbox` bővül — priority queue implementáció. Graceful shutdown szekvencia a supervision tree-vel.
> ~3-4 új fájl, ~200-300 sor runtime, ~300-400 sor teszt.
>
> en: `IMailbox` extended — priority queue implementation. Graceful shutdown sequence with supervision tree.
> ~3-4 new files, ~200-300 lines runtime, ~300-400 lines test.

---

## Phase 5 — Security & Compliance

> hu: Capability-based security — ami Akka.NET-ből és Orleans-ból hiányzik, és ami eladja a terméket.
>
> en: Capability-based security — what Akka.NET and Orleans lack, and what sells the product.

---

## M5.1 — Full Capability Model

> hu: Teljes capability életciklus: kiadás, ellenőrzés, audit trail.
>
> en: Full capability lifecycle: issuance, verification, audit trail.

| Elem / Item | Leírás / Description |
|---|---|
| `TCapability` ≡ `TActorRef` | hu: A capability a CST (Capability Slot Table) HW táblában van, a ref egy opaque 32-bit index (`TActorRef(int SlotIndex)`). NEM külön struct. / en: The capability resides in the CST (Capability Slot Table) HW table; the ref is an opaque 32-bit index (`TActorRef(int SlotIndex)`). NOT a separate struct. |
| CST HW lookup | hu: Célcore mailbox-edge HW unit CST lookup minden Send-nél. / en: Target-core mailbox-edge HW unit CST lookup on every Send. |
| Permission bit-flag | hu: Send, Stop, Watch, Delegate, Revoke, Query, Snapshot, Migrate — a CST-ben tárolt 8 bit. / en: Send, Stop, Watch, Delegate, Revoke, Query, Snapshot, Migrate — 8 bits stored in CST. |
| Runtime check | hu: Célcore mailbox-edge HW unit CST lookup minden Send-nél. / en: Target-core mailbox-edge HW unit CST lookup on every Send. |
| Audit trail | hu: Érvénytelen CST slot + AuthCode quarantine események naplózva. / en: Invalid CST slot + AuthCode quarantine events logged. |

**CFPU:**
> hu: A célcore mailbox-edge HW unit CST lookup-ot végez (NEM az interconnect router). Érvénytelen CST slot → drop + fail-stop + AuthCode quarantine. Részletek: [trust-model-hu.md](trust-model-hu.md). (**Megjegyzés:** osreq-007 OBSOLETE — a CST modell váltja fel.)
>
> en: The target-core mailbox-edge HW unit performs CST lookup (NOT the interconnect router). Invalid CST slot → drop + fail-stop + AuthCode quarantine. Details: [trust-model-en.md](trust-model-en.md). (**Note:** osreq-007 is OBSOLETE — superseded by the CST model.)

**Becsült óra / Est. hours:** ~28-36
> hu: M2.5 Capability Registry-re épül. CST lookup teljesítmény kritikus — minden Send-nél fut.
> ~4-5 új fájl, ~300-400 sor runtime, ~400-500 sor teszt.
>
> en: Builds on M2.5 Capability Registry. CST lookup performance is critical — runs on every Send.
> ~4-5 new files, ~300-400 lines runtime, ~400-500 lines test.

---

## M5.2 — Capability Delegation & Revocation

> hu: Finomhangolt capability megosztás és visszavonás — attenuation elvével.
>
> en: Fine-grained capability sharing and revocation — with the principle of attenuation.

| Elem / Item | Leírás / Description |
|---|---|
| Delegation | hu: Actor üzenetben küldi tovább a ref-et — a fogadó megkapja a capability-t. / en: Actor forwards ref in message — receiver gets the capability. |
| Attenuation | hu: Delegáláskor szűkíthető a permission (Write → Read-only). / en: Permission can be narrowed on delegation (Write → Read-only). |
| Revocation | hu: A kiadó érvényteleníti — minden klón egyszerre válik érvénytelenné. / en: Issuer invalidates — all clones become invalid simultaneously. |
| Revocation broadcast | hu: Registry értesíti az érintett router-eket. / en: Registry notifies affected routers. |

**Becsült óra / Est. hours:** ~20-28
> hu: M5.1-re épül. Revocation broadcast a teljes router hálón — performance szempontból kényes.
> ~3-4 új fájl, ~200-300 sor runtime, ~300-400 sor teszt.
>
> en: Builds on M5.1. Revocation broadcast across the full router network — performance-sensitive.
> ~3-4 new files, ~200-300 lines runtime, ~300-400 lines test.

---

## M5.3 — Audit & Compliance Logging

> hu: Tamper-proof audit napló minden capability műveletről — compliance-ready.
>
> en: Tamper-proof audit log for every capability operation — compliance-ready.

| Elem / Item | Leírás / Description |
|---|---|
| Audit logger actor | hu: Minden capability művelet naplózva — maga is capability-protected. / en: Every capability operation logged — itself capability-protected. |
| Structured log format | hu: JSON / OpenTelemetry kompatibilis kimenet. / en: JSON / OpenTelemetry compatible output. |
| Tamper-proof | hu: A log actor-t nem írhatja felül senki — csak append. / en: Nobody can overwrite the log actor — append only. |
| Compliance report | hu: IEC 61508, ISO 26262 alapszintű reportálás. / en: IEC 61508, ISO 26262 baseline reporting. |

**Becsült óra / Est. hours:** ~20-28
> hu: A compliance report a certification path szempontjából kritikus (medical, automotive, aviation).
> ~3-4 új fájl, ~200-300 sor runtime, ~200-300 sor teszt.
>
> en: Compliance report is critical for the certification path (medical, automotive, aviation).
> ~3-4 new files, ~200-300 lines runtime, ~200-300 lines test.

---

## M5.4 — Formal Verification Foundations

> hu: Kernel actor specifikáció formális modellben — a certification path alapja.
>
> en: Kernel actor specification in formal model — foundation of the certification path.

| Elem / Item | Leírás / Description |
|---|---|
| TLA+ / Alloy specifikáció | hu: Kernel actor-ok formális leírása. / en: Formal description of kernel actors. |
| Invariáns definíciók | hu: Capability soha nem hamisítható, actor izolálva marad. / en: Capability never forgeable, actor always isolated. |
| Model checking | hu: Root supervisor, scheduler, router core kernel actors-ra. / en: Applied to root supervisor, scheduler, router core kernel actors. |

**Becsült óra / Est. hours:** ~32-48
> hu: Legbizonytalanabb becslés — TLA+ / Alloy tanulási görbe. Nem teljes formális proof (az seL4 szintű), de az alapok lefektetve.
>
> en: Most uncertain estimate — TLA+ / Alloy learning curve. Not a full formal proof (seL4 level), but foundations laid.

---

## Phase 6 — Developer Experience

> hu: Senki nem vesz meg egy framework-öt, ha nem kellemes használni.
>
> en: Nobody buys a framework that isn't pleasant to use.

---

## M6.1 — CLI Tooling

> hu: `symphact` parancssori eszköz — project template-ek, build, run, deploy, monitor.
>
> en: `symphact` command-line tool — project templates, build, run, deploy, monitor.

| Elem / Item | Leírás / Description |
|---|---|
| `symphact` CLI | hu: `new`, `build`, `run`, `deploy`, `monitor` parancsok. / en: `new`, `build`, `run`, `deploy`, `monitor` commands. |
| `dotnet new symphact-app` | hu: Console actor app template. / en: Console actor app template. |
| `dotnet new symphact-service` | hu: Supervised service template. / en: Supervised service template. |
| `dotnet new symphact-device` | hu: Device actor template. / en: Device actor template. |

**Becsült óra / Est. hours:** ~20-28
> hu: Új repo vagy tool projekt. dotnet template engine integrálás.
> ~4-5 új fájl, ~300-400 sor CLI kód, ~200-300 sor teszt.
>
> en: New repo or tool project. dotnet template engine integration.
> ~4-5 new files, ~300-400 lines CLI code, ~200-300 lines test.

---

## M6.2 — IDE Integration

> hu: Visual Studio Code extension + Rider plugin — actor tree vizualizáció, message trace.
>
> en: Visual Studio Code extension + Rider plugin — actor tree visualization, message trace.

| Elem / Item | Leírás / Description |
|---|---|
| VS Code extension | hu: Actor state inspector, message trace viewer. / en: Actor state inspector, message trace viewer. |
| Rider plugin | hu: JetBrains Rider integráció. / en: JetBrains Rider integration. |
| Live actor tree | hu: Supervision hierarchia vizualizáció valós időben. / en: Real-time supervision hierarchy visualization. |
| Message breakpoint | hu: Töréspontot lehet tenni message receive-re (nem csak kódsorhoz). / en: Breakpoint can be set on message receive (not just line of code). |

**Becsült óra / Est. hours:** ~24-32
> hu: Legnagyobb tanulási görbe — VS Code extension API, JetBrains Platform SDK. Két külön plugin projekt.
>
> en: Steepest learning curve — VS Code extension API, JetBrains Platform SDK. Two separate plugin projects.

---

## M6.3 — Profiler & Monitoring Dashboard

> hu: Actor teljesítmény mérés, rendszer dashboard, Prometheus + Grafana export.
>
> en: Actor performance measurement, system dashboard, Prometheus + Grafana export.

| Elem / Item | Leírás / Description |
|---|---|
| Actor profiler | hu: msg/sec per actor, latency histogram, mailbox depth. / en: msg/sec per actor, latency histogram, mailbox depth. |
| System dashboard | hu: Web UI: actor tree, message flow, resource usage. / en: Web UI: actor tree, message flow, resource usage. |
| Prometheus export | hu: Standard metrics endpoint. / en: Standard metrics endpoint. |
| Grafana template | hu: Előre konfigurált dashboard sablon. / en: Pre-configured dashboard template. |

**Becsült óra / Est. hours:** ~24-32
> hu: Web UI a legmunkaigényesebb rész. Prometheus client library integrálás egyszerű.
> ~5-6 új fájl / komponens, ~400-500 sor kód.
>
> en: Web UI is the most labor-intensive part. Prometheus client library integration is straightforward.
> ~5-6 new files / components, ~400-500 lines code.

---

## M6.4 — NuGet Packages & API Reference

> hu: Kiadható NuGet csomagok és teljes API dokumentáció.
>
> en: Publishable NuGet packages and full API documentation.

| Elem / Item | Leírás / Description |
|---|---|
| `Symphact.Core` | hu: Runtime primitívek. / en: Runtime primitives. |
| `Symphact.Supervision` | hu: Supervisor strategies. / en: Supervisor strategies. |
| `Symphact.Persistence` | hu: Event sourcing. / en: Event sourcing. |
| `Symphact.Remoting` | hu: Distribution. / en: Distribution. |
| `Symphact.Security` | hu: Capability model. / en: Capability model. |
| `Symphact.Devices` | hu: Device actor framework. / en: Device actor framework. |
| API reference site | hu: DocFX vagy hasonló eszközzel generálva. / en: Generated with DocFX or similar. |

**Becsült óra / Est. hours:** ~16-24
> hu: NuGet packaging viszonylag egyszerű — a csomagok már meglévő projektek. Az API site generálás automatizált.
>
> en: NuGet packaging is relatively straightforward — packages are already existing projects. API site generation is automated.

---

## M6.5 — Documentation & Tutorials

> hu: Fejlesztői dokumentáció és tutorial sorozat — onboarding 30 perc alatt.
>
> en: Developer documentation and tutorial series — onboarding in 30 minutes.

| Elem / Item | Leírás / Description |
|---|---|
| Getting started | hu: 30 perc alatt első actor app. / en: First actor app in 30 minutes. |
| Architecture deep-dive | hu: A vision doc fejlesztői verziója. / en: Developer version of the vision doc. |
| Migration guide | hu: Akka.NET-ről / Orleans-ról való átállás. / en: Migration from Akka.NET / Orleans. |
| Tutorial sorozat / Tutorial series | hu: IoT gateway, AI agent cluster, SNN demo. / en: IoT gateway, AI agent cluster, SNN demo. |
| FAQ + troubleshooting | hu: Gyakori problémák és megoldások. / en: Common issues and solutions. |

**Becsült óra / Est. hours:** ~16-24
> hu: Dokumentáció írás időigényes de nem technikai nehézségű. A migration guide stratégiailag fontos — ez nyitja meg az Akka.NET / Orleans piacot.
>
> en: Documentation writing is time-consuming but not technically difficult. Migration guide is strategically important — it opens the Akka.NET / Orleans market.

---

## Phase 7 — Első Eladható Termék / First Sellable Product

> hu: A termék, amire egy cég rábólintana: "ezt megveszem / erre fizetek support-ot."
>
> en: The product a company would nod at: "I'll buy this / I'll pay for support."

---

## M7.1 — Reference Applications

> hu: Négy referencia alkalmazás — a framework képességeinek teljes bemutatása.
>
> en: Four reference applications — full demonstration of the framework's capabilities.

| Elem / Item | Leírás / Description |
|---|---|
| IoT Gateway | hu: Sensor actors → protocol actors → cloud actor (MQTT/HTTP). / en: Sensor actors → protocol actors → cloud actor (MQTT/HTTP). |
| AI Agent Cluster | hu: Supervisor hierarchy + capability-secured LLM agents. / en: Supervisor hierarchy + capability-secured LLM agents. |
| SNN Demo | hu: 1000 LIF neuron actor, real-time spike propagation. / en: 1000 LIF neuron actors, real-time spike propagation. |
| Chat Server | hu: Multi-room, multi-user, hot code update demóval. / en: Multi-room, multi-user, with hot code update demo. |

**Becsült óra / Est. hours:** ~24-32
> hu: Minden referencia app egyszerre marketing anyag és integrációs teszt. Az SNN Demo a CFPU vision közvetlen megvalósítása.
>
> en: Every reference app is simultaneously marketing material and integration test. SNN Demo is the direct realization of the CFPU vision.

---

## M7.2 — Production Hardening

> hu: Stressz- és káosz tesztelés — a rendszer tartja magát extrém körülmények között.
>
> en: Stress and chaos testing — the system holds up under extreme conditions.

| Elem / Item | Leírás / Description |
|---|---|
| Stress testing | hu: 10K+ actor, sustained load — teljesítmény határ meghatározás. / en: 10K+ actors, sustained load — performance boundary determination. |
| Chaos testing | hu: Random actor kill, network partition szimulálás. / en: Random actor kill, network partition simulation. |
| Memory leak detection | hu: Long-running soak test. / en: Long-running soak test. |
| Performance regression CI | hu: Benchmark minden PR-nél — regresszió automatikusan kiderül. / en: Benchmark on every PR — regression detected automatically. |

**Becsült óra / Est. hours:** ~20-28
> hu: A chaos testing infrastruktúra felépítése a legmunkaigényesebb. A CI benchmark integrálás automatikus riasztással kritikus.
>
> en: Building the chaos testing infrastructure is the most labor-intensive. CI benchmark integration with automatic alerting is critical.

---

## M7.3 — Enterprise Features

> hu: RBAC, multi-tenant izoláció, konfiguráció kezelés, K8s health check.
>
> en: RBAC, multi-tenant isolation, configuration management, K8s health check.

| Elem / Item | Leírás / Description |
|---|---|
| RBAC | hu: Role → capability set mapping. / en: Role → capability set mapping. |
| Multi-tenant izoláció | hu: Tenant = supervision subtree — szigorú izoláció. / en: Tenant = supervision subtree — strict isolation. |
| Config management | hu: Actor-alapú konfigurációs service. / en: Actor-based configuration service. |
| Health check API | hu: K8s liveness/readiness probe kompatibilis. / en: K8s liveness/readiness probe compatible. |

**Becsült óra / Est. hours:** ~16-24
> hu: Az RBAC a capability model természetes kiterjesztése. K8s health check viszonylag egyszerű — HTTP endpoint az actor systemre.
>
> en: RBAC is a natural extension of the capability model. K8s health check is relatively simple — HTTP endpoint on the actor system.

---

## M7.4 — Licensing & Packaging

> hu: Dual license, Docker image, Helm chart, landing page.
>
> en: Dual license, Docker image, Helm chart, landing page.

| Elem / Item | Leírás / Description |
|---|---|
| Dual license | hu: Apache-2.0 (community) + Commercial (enterprise support). / en: Apache-2.0 (community) + Commercial (enterprise support). |
| Docker image | hu: `symphactos-runtime` — production-ready container. / en: `symphactos-runtime` — production-ready container. |
| Helm chart | hu: K8s deployment template. / en: K8s deployment template. |
| Landing page | hu: symphact.org — termék bemutató oldal. / en: symphact.org — product presentation page. |

**Becsült óra / Est. hours:** ~8-12
> hu: A legrövidebb milestone — főleg koordináció és marketing feladatok. A dual license jogi szöveg a kritikus elem.
>
> en: Shortest milestone — mostly coordination and marketing tasks. The dual license legal text is the critical element.

---

## M7.5 — Launch Preparation

> hu: Nyilvános megjelenés — blog, konferencia, GitHub Sponsors, első partnerek.
>
> en: Public launch — blog, conference, GitHub Sponsors, first partners.

| Elem / Item | Leírás / Description |
|---|---|
| Announcement blog post | hu: A termék és a vision bemutatása. / en: Product and vision presentation. |
| Conference talk | hu: NDC, DotNext, Strange Loop anyagok. / en: NDC, DotNext, Strange Loop materials. |
| GitHub Sponsors / Open Collective | hu: Community support csatorna. / en: Community support channel. |
| Enterprise support SLA | hu: Formális support szerződés dokumentum. / en: Formal support contract document. |
| First partner onboarding | hu: 1-2 early adopter cég. / en: 1-2 early adopter companies. |

**Becsült óra / Est. hours:** ~12-16
> hu: A partner onboarding a legfontosabb — az első fizető ügyfelek visszajelzése formálja a Phase 8-at (ha lesz).
>
> en: Partner onboarding is most important — the first paying customers' feedback shapes Phase 8 (if any).

---

## Changelog

| Version | Date | Summary |
|---------|------|---------|
| 1.0 | 2026-04-18 | Initial release |
