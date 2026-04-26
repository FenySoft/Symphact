# Menetrend

> Az aktor-runtime fejlesztési mérföldkövei — a CFPU hardverrel együtt-tervezve.

## Összesítés

| Fázis | Milestone-ok | Becsült óra | Mit ad? |
|-------|-------------|-------------|---------|
| **Phase 1** Core Runtime | M0.1-M1.0 | ~148-204 | Actor runtime + CFPU ref impl |
| **Phase 2** Kernel Foundation | M2.1-M2.5 | ~120-160 | Scheduler, router, memory, capabilities |
| **Phase 3** Device & I/O | M3.1-M3.5 | ~80-120 | Device actors (UART, GPIO, timer, storage) |
| **Phase 4** Advanced Runtime | M4.1-M4.4 | ~100-140 | Hot code loading, migration, backpressure |
| **Phase 5** Security | M5.1-M5.4 | ~100-140 | Capability security, audit, formal verification |
| **Phase 6** Developer Experience | M6.1-M6.5 | ~100-140 | CLI, IDE, profiler, NuGet, docs |
| **Phase 7** Első Eladható Termék | M7.1-M7.5 | ~80-120 | Reference apps, enterprise, launch |
| **Összesen** | | **~728-1024** | **Production-ready Symphact** |

> ~728-1024 óra ≈ **4-6 hónap full-time** (1 ember) vagy **2-3 hónap** (ügynök csapattal).

### Phase 1 — Core Runtime részletezés (M0.1-M1.0)

| Mérföldkő | Állapot | Becsült óra | Komplexitás |
|-----------|---------|-------------|-------------|
| M0.1 | ✅ | ~6 | Közepes |
| M0.2 | ✅ | ~8 (tény) | Közepes |
| M0.3 | Tervezett | ~24-32 | Magas |
| M0.4 | Tervezett | ~24-32 | Magas |
| M0.5 | Tervezett | ~16-24 | Közepes-magas |
| M0.6 | Tervezett | ~28-36 | Magas |
| M0.7 | Tervezett | ~30-50 | Nagyon magas |
| M1.0 | Tervezett | ~12-16 | Közepes |
| **Összesen** | | **~148-204** | |

> A becslések az M0.2 tényadataira épülnek (~8 óra, 580 sor, 46 teszt, közepes komplexitás).
> Az ügynök csapat (Architect + Implementer + Devil's Advocate + Test Guardian + HW Liaison) munkaóráit emberi egyenértékben adjuk meg.

---

## M0.1 — Alapprimitívek ✅

> Mailbox, actor ref, actor, actor system.

| Elem | Leírás |
|---|---|
| `IMailbox` / `TMailbox` | FIFO mailbox (lock-free MPMC, `ConcurrentQueue`). |
| `TActorRef` | Capability token (`readonly record struct`, 64 bit). |
| `TActor<TState>` | Absztrakt aktor: `Init()` + `Handle(state, msg)`. |
| `TActorSystem` | Runtime: `Spawn`, `Send`, `DrainAsync`, `GetState` (teszt). |

**Tesztek:** 30 xUnit | **Becsült óra:** ~6

---

## M0.2 — ActorContext + Inter-Actor Messaging ✅

> Aktorok üzeneteket küldhetnek egymásnak a handler-ből.

| Elem | Leírás |
|---|---|
| `IActorContext` | Handler kontextus: `Self` (saját ref) + `Send(target, msg)`. |
| `TActorContext` | Readonly struct referencia impl — delegál `TActorSystem.Send`-re. |
| Handle signature | `Handle(TState, object, IActorContext)` — context paraméterrel. |
| `DrainAsync` MaxRounds | Végtelen-loop védelem (default: 1000 kör). |

**CFPU:** A Send path allokáció-mentes a routing-ban. Az `IActorContext` elég szűk MMIO HW FIFO mögé is. `Spawn` szándékosan kimaradt — az M0.3 supervision határozza meg.

**Tesztek:** 46 xUnit (16 új) — 100% dotCover | **Tény óra:** ~8

---

## M0.3 — Supervision (Let-it-crash)

> Hibakezelés supervisor stratégiákkal, actor hierarchia.

| Elem | Leírás |
|---|---|
| `ISupervisorStrategy` | Resume, Restart, Stop, Escalate döntések. |
| `TOneForOneStrategy` | Csak a hibás child-ot kezeli. |
| `TAllForOneStrategy` | Minden child-ot újraindítja. |
| Actor hierarchia | Parent-child viszony — `Spawn` a context-en keresztül. |
| Lifecycle hook-ok | `PreStart`, `PostStop`, `PreRestart`, `PostRestart`. |

**CFPU:** A restart = core reset + mailbox flush. A supervisor strategy-nek nem szabad feltételezni, hogy a restart olcsó — CFPU-n HW core reset kell.

**Lehetséges HW kérés:** Core reset mechanizmus: van-e HW support egy core SRAM + mailbox tisztítására?

**Becsült óra:** ~24-32
> A legösszetettebb milestone eddig. Új fogalmak: supervisor strategy, actor hierarchia (parent-child), lifecycle hook-ok. Mélyen érinti a TActorSystem-et (spawn hierarchikussá válik, DrainAsync error handling, restart logika). IActorContext bővül Spawn-nal. ~5-7 új fájl, ~300-400 sor runtime, ~400-600 sor teszt.

---

## M0.4 — Scheduler + Per-Actor Parallelism

> Több actor párhuzamos futtatása (single-host, multi-thread).

| Elem | Leírás |
|---|---|
| `IScheduler` | Ütemező absztrakció. |
| `TRoundRobinScheduler` | Referencia implementáció. |
| `TDedicatedThreadScheduler` | Per-actor thread — CFPU core szimuláció. |

**CFPU:** Ez a legközelebb a CFPU valóságához: minden core fizikailag egy actor, saját SRAM-mal és HW mailbox FIFO-val. A `TDedicatedThreadScheduler` a CFPU-t szimulálja .NET thread-ekkel. CFPU-n nincs "scheduling" — minden core mindig fut.

**Lehetséges HW kérés:** Mailbox interrupt vs polling: a core hogyan értesül új üzenetről?

**Becsült óra:** ~24-32
> A DrainAsync alapvetően megváltozik: single-threaded → multi-threaded. Thread safety mindenhol kritikussá válik. Concurrency tesztelés inherensen nehéz (race condition-ök, deadlock-ok). ~3-4 új fájl, ~200-300 sor runtime, ~300-500 sor teszt.

---

## M0.5 — Persistence (Event Sourcing)

> Actor state túléli a restart-ot.

| Elem | Leírás |
|---|---|
| `IPersistenceProvider` | Journal absztrakció. |
| `TInMemoryJournal` | Teszt implementáció. |
| `TSqliteJournal` | Referencia implementáció. |
| Snapshot + Replay | Állapot-pillanatképek és visszajátszás. |

**CFPU:** Core SRAM volatile — tápelvesztésnél elveszik. Persistence = DMA kiírás külső DRAM/flash-re. A journal interfésznek aszinkron, nem-blokkoló kiírást kell támogatnia.

**Lehetséges HW kérés:** DMA engine hozzáférés core-onként: saját DMA vagy központi controller?

**Becsült óra:** ~16-24
> Új projekt lehetséges (Symphact.Persistence). Külső függőség (SQLite). Snapshot + replay logika. Integráció M0.3 supervision-nel (recovery restart után). ~4-5 új fájl, ~300-400 sor runtime, ~400-500 sor teszt.

---

## M0.6 — Remoting / Distribution

> Actor-ok hálózaton / chip-ek között kommunikálnak.

| Elem | Leírás |
|---|---|
| `TActorRef` kiterjesztés | Location info (chip-id + core-id + offset). |
| Serialization layer | HW message formátummal kompatibilis. |
| `ITransport` / `TTcpTransport` | Transport absztrakció + TCP referencia. |

**CFPU:** Multi-chip topológia = distribution a HW szinten. A `TActorRef` formátumnak tartalmaznia kell chip-id + core-id + mailbox offset-et. Fix méretű header + payload.

**Lehetséges HW kérés:** Inter-chip protokoll: milyen link? Max message méret? HW routing (mesh NoC)?

**Becsült óra:** ~28-36
> TActorRef formátum változás (location info). Serialization layer (message → byte → message). Hálózati kód: connection management, reconnection, hibakezelés. Location-transparent Send routing. Új projekt (Symphact.Remoting). ~5-7 új fájl, ~500-700 sor runtime, ~500-700 sor teszt.

---

## M0.7 — CFPU Hardware Integration

> Hardver FIFO integráció — az OS találkozik a szilíciummal.

| Elem | Leírás |
|---|---|
| `TMmioMailbox` | MMIO-alapú HW FIFO mailbox implementáció. |
| `TActorRef` végleges formátum | `[HMAC:24][perms:8][actor-id:8][core-coord:24]` = 64 bit (chip-local). Bit-azonos a CLI-CPU 16 byte interconnect header alsó 64 bitjével. Részletek: [actor-ref-scaling-hu.md](actor-ref-scaling-hu.md), [osreq-007](osreq-to-cfpu/osreq-007-actor-ref-format-hu.md). |
| CLI-CPU szinkronizáció | `osreq-to-cfpu` feedback loop a HW repo-val. |

**Becsült óra:** ~30-50
> A legbizonytalanabb becslés — függ a CLI-CPU HW készültségétől. MMIO register hozzáférés .NET-ből (unsafe kód vagy P/Invoke). TActorRef végleges bit layout. Hardver teszteléshez FPGA vagy szimulátor kell. ~3-4 új fájl, ~200-400 sor runtime. Teszt korlátozott (HW-függő tesztek szimulátort igényelnek).

---

## M1.0 — Samples + Stabilizáció

> Használható referencia runtime, dokumentációval és benchmarkokkal.

| Elem | Leírás |
|---|---|
| CounterActor sample | Egyszerű számláló minta alkalmazás. |
| ChatRoom sample | Multi-actor minta (üzenetkezelés). |
| API reference | Teljes publikus API dokumentáció. |
| Benchmarks | Teljesítmény mérések (msg/sec, latency). |

**Becsült óra:** ~12-16
> Minta alkalmazások, API dokumentáció generálás, teljesítmény mérések. Nincs új runtime logika — a meglévő API-k stabilizálása és bemutatása.

---

## Phase 2 — Kernel Foundation (M2.1-M2.5)

> Az actor runtime-ből valódi "OS" lesz — kernel aktorok kezelik a rendszer erőforrásait.

**Összesen: ~120-160 óra**

### M2.1 — Root Supervisor + Actor Hierarchia Kezelés

> A rendszer csúcsán álló `TRootSupervisor` — minden más actor ebből a fából nő ki.

| Elem | Leírás |
|---|---|
| `TRootSupervisor` | Boot-kor indul, soha nem crash-el. |
| Actor tree kezelés | Parent-child lekérdezés, tree traversal. |
| System-level stratégia | A root mindig restart-ol. |
| Boot szekvencia | root → kernel_sup → app_sup. |

**Építő elem:** M0.3 supervision-re épül.

**CFPU:** A boot szekvencia a Rich core-on fut — a root supervisor az első actor.

**Becsült óra:** ~20-28

---

### M2.2 — Scheduler Actor

> Aktorok core-okhoz rendelése, terhelés-elosztás.

| Elem | Leírás |
|---|---|
| `ISchedulerPolicy` | Pluggable ütemezési stratégia. |
| `TRoundRobinPolicy` | Körben osztja el az actor futtatást. |
| `TLoadBalancePolicy` | Terhelés alapján dönt. |
| Actor-to-core affinity | Az actor ragad egy core-on, ha kéri. |
| Cooperative scheduling | Actor yield-el message boundary-n. |
| Watchdog | Ha egy actor nem yield-el N ms-en belül, supervisor értesítés. |

**Építő elem:** M0.4 scheduler-re épül, de most actor-szintű döntések.

**CFPU:** Cooperative + watchdog timer HW interrupt. CFPU-n nincs preemption — minden core mindig az ő actor-ját futtatja.

**Becsült óra:** ~28-36

---

### M2.3 — Router Actor

> Logikai actor ref → fizikai cím (core + mailbox offset) feloldás.

| Elem | Leírás |
|---|---|
| `TRouter` actor | Ref resolution, cache. |
| Location-transparent routing | Local, inter-core, remote — ugyanaz a Send. |
| Actor migration support | A router frissíti a mappinget amikor actor core-t vált. |
| Dead letter handling | Érvénytelen ref → dead letter actor-nak (nem exception). |

**Építő elem:** M0.6 remoting transport-ra épül.

**CFPU:** A HW router a chip-en belüli routing-ot végzi; a SW router a chip-ek közöttit.

**Becsült óra:** ~24-32

---

### M2.4 — Memory Manager

> Per-core memóriakezelés + Rich core heap pool.

| Elem | Leírás |
|---|---|
| `TMemoryManager` actor | Heap pool allokáció Rich core-oknak. |
| Per-core SRAM budget | CFPU: fix méret, .NET: szimulált limit. |
| GC trigger policy | Per-actor GC, nincs global stop-the-world. |
| Memory pressure értesítés | Ha egy actor túl sok memóriát használ → supervisor értesítés. |

**CFPU:** Minden core saját 16-256 KB SRAM. Nincs shared heap. A memory manager csak a Rich core heap pool-t kezeli.

**Becsült óra:** ~20-28

---

### M2.5 — Capability Registry

> Actor ref mint capability token: kiadás, delegálás, visszavonás.

| Elem | Leírás |
|---|---|
| `TCapabilityRegistry` actor | Capability-k nyilvántartása. |
| `TActorRef` ≡ `TCapability` | A ref maga a capability — egyetlen 64 bit token: `[HMAC:24][perms:8][actor-id:8][core-coord:24]`. Bit-azonos a CLI-CPU header alsó 64 bitjével. Részletek: [actor-ref-scaling-hu.md](actor-ref-scaling-hu.md). |
| Capability kiadás | Spawn-kor a registry SipHash-128 MAC-cel aláírja. |
| Delegálás | Actor továbbadhatja a ref-et üzenetben (a perms-szel együtt — attenuation lehetséges). |
| Visszavonás | Per-chip kulcsrotáció (event-driven, hibás-HMAC counter threshold átlépésére). |
| Permission bit-flag | Send, Stop, Watch, Delegate, Revoke, Query, Snapshot, Migrate. |
| AuthCode integráció | Spawn-time aláíró-blacklist + bytecode SHA blacklist check. |

**Építő elem:** M0.6 TActorRef kiterjesztésre épül, és a CFPU AuthCode rendszerre (`CLI-CPU/docs/authcode-hu.md`).

**CFPU:** A célcore mailbox-edge HW unit ellenőrzi az HMAC-ot minden Send-nél (NEM az interconnect router — a "egy mailbox IRQ per core" elv). Hamis HMAC → drop + fail-stop a küldő core-on + AuthCode quarantine az aláíró ellen. Részletek: [osreq-007](osreq-to-cfpu/osreq-007-actor-ref-format-hu.md), [trust-model-hu.md](trust-model-hu.md).

**Becsült óra:** ~28-36

---

## Phase 3 — Device & I/O Layer (M3.1-M3.5)

> Minden HW periféria actor — az alkalmazás capability-n keresztül éri el.

**Összesen: ~80-120 óra**

### M3.1 — Device Actor Framework

| Elem | Leírás |
|---|---|
| `IDeviceActor` interfész | MMIO régió tulajdonjog, interrupt kezelés. |
| `TDeviceActorBase<TState>` | Absztrakt alap device actor osztály. |
| MMIO absztrakció | `Read(address)`, `Write(address, value)` — .NET-en szimulált, CFPU-n valódi. |
| Interrupt → mailbox | Interrupt konverzió mailbox message-re. |
| Device capability | Csak az fér hozzá, akinek van ref-je. |

**CFPU:** Minden device actor egy Rich core-on fut, MMIO régiót birtokol.

**Becsült óra:** ~20-28

---

### M3.2 — UART Device Actor

| Elem | Leírás |
|---|---|
| TX/RX actor pár | Adás és vétel szétválasztva. |
| Subscribe pattern | Alkalmazás feliratkozik RX byte-okra. |
| Backpressure | TX buffer tele → sender blokkolódik. |

**Becsült óra:** ~12-16

---

### M3.3 — GPIO Device Actor

| Elem | Leírás |
|---|---|
| Pin konfigurálás | Input / output / interrupt mód. |
| Edge-triggered interrupt | Interrupt → message konverzió. |

**Becsült óra:** ~8-12

---

### M3.4 — Timer / Clock Service

| Elem | Leírás |
|---|---|
| `TTimerService` actor | Rendszer-szintű időzítő. |
| Tick üzenetek | Konfigurálható intervallummal. |
| Timeout pattern | `AskWithTimeout` megvalósítás. |
| Mock clock | Determinisztikus idő teszteléshez. |

**Becsült óra:** ~12-16

---

### M3.5 — Storage Service

> Nem POSIX byte-stream, hanem actor-alapú strukturált tárolás.

| Elem | Leírás |
|---|---|
| `IStorageProvider` interfész | Tárolási absztrakció. |
| `TFileSystemActor` | Actor-alapú storage (nem POSIX). |
| File handle = actor | Open = spawn, close = stop. |
| Flash device actor | QSPI flash támogatás. |

**Nem POSIX:** `Send(fsActor, new ReadMsg(path))` — nem `open()`/`read()`.

**Becsült óra:** ~28-36

---

## Phase 4 — Advanced Runtime (M4.1-M4.4)

> A "valódi OS" feature-ök amik a versenytársaktól megkülönböztetnek.

**Összesen: ~100-140 óra**

### M4.1 — Hot Code Loading

> Erlang-style zero-downtime actor frissítés.

| Elem | Leírás |
|---|---|
| `THotCodeLoader` actor | CIL binary fogadás, verifikálás, betöltés. |
| Opcode whitelist | Biztonsági ellenőrzés betöltéskor. |
| Verzióváltás | Actor-szintű, message boundary-n. |
| State preserváció | State megmarad version upgrade-nél. |

**CFPU:** Writable microcode SRAM (F6+) — új opcode szemantika runtime-ban.

**Ez a killer feature:** Zero-downtime update, amit Akka.NET/Orleans nem tud.

**Becsült óra:** ~32-40

---

### M4.2 — Actor Migration

| Elem | Leírás |
|---|---|
| Suspend → serialize → move | Actor áthelyezés core-ok között. |
| Router mapping frissítés | A ref ugyanaz marad mozgatás után. |
| Cross-core migration | .NET: thread pool alapú. |
| Cross-node migration | Remoting-on keresztül. |
| Load balancing trigger | A scheduler dönt mikor migráljunk. |

**CFPU:** Nano→Rich migration trap-on keresztül.

**Becsült óra:** ~24-32

---

### M4.3 — Backpressure + Flow Control

| Elem | Leírás |
|---|---|
| Mailbox depth limit | Konfigurálható per-actor. |
| Mailbox tele → blokk | Sender blokkolódik VAGY `SendError` trap. |
| `TrySend` | Non-blocking, bool visszatérési értékkel. |
| Backpressure propagáció | A supervision tree-n felfelé terjed. |

**CFPU:** HW FIFO depth fix (8-64) — természetes backpressure.

**Becsült óra:** ~20-28

---

### M4.4 — Priority Mailbox + System Messages

| Elem | Leírás |
|---|---|
| `TPriorityMailbox` | System messages queue-t megelőznek. |
| Opt-in per actor | Default: FIFO. |
| Watchdog timeout | High-priority kill message. |
| Graceful shutdown | Root → leaf sorrend, priority messages-szel. |

**Becsült óra:** ~24-32

---

## Phase 5 — Security & Compliance (M5.1-M5.4)

> Capability-based security — ami Akka.NET-ből és Orleans-ból hiányzik, és ami eladja a terméket.

**Összesen: ~100-140 óra**

### M5.1 — Teljes Capability Modell

| Elem | Leírás |
|---|---|
| `TCapability` ≡ `TActorRef` | A capability ÉS a ref ugyanaz a 64 bit token. Layout: `[HMAC:24][perms:8][actor-id:8][core-coord:24]` (lásd [actor-ref-scaling-hu.md](actor-ref-scaling-hu.md)). NEM külön struct. |
| SipHash-128 MAC | capability_registry per-core kulccsal, MSB-truncate 24 bit. |
| Permission bit-flag | Send, Stop, Watch, Delegate, Revoke, Query, Snapshot, Migrate. |
| Runtime ellenőrzés | Célcore mailbox-edge HW unit minden Send-nél (osreq-007). |
| Audit trail | Hibás HMAC + AuthCode quarantine események naplózva. |

**Becsült óra:** ~28-36

---

### M5.2 — Capability Delegálás és Visszavonás

| Elem | Leírás |
|---|---|
| Delegálás | Actor üzenetben küldi tovább a ref-et. |
| Attenuation | Delegáláskor szűkíthető a permission (Write → Read-only). |
| Visszavonás | A kiadó (vagy admin) érvényteleníti az összes klónt. |
| Revocation broadcast | A registry értesíti az érintett router-eket. |

**Becsült óra:** ~20-28

---

### M5.3 — Audit & Compliance Naplózás

| Elem | Leírás |
|---|---|
| Actor-alapú audit logger | Minden capability művelet naplózva. |
| Strukturált log formátum | JSON / OpenTelemetry kompatibilis. |
| Tamper-proof | A log actor capability-protected, nem írhatja felül senki. |
| Compliance riport | IEC 61508, ISO 26262 alap. |

**Becsült óra:** ~20-28

---

### M5.4 — Formális Verifikáció Alapok

| Elem | Leírás |
|---|---|
| Kernel actor specifikáció | TLA+ vagy Alloy formalizmus. |
| Invariánsok | Capability nem hamisítható, actor izolált. |
| Model checking | Core kernel actors-ra (root supervisor, scheduler, router). |

> Nem teljes formális proof (az seL4 szintű), de az alapok lefektetve.

**Ez a certification path:** Medical, automotive, aviation vásárlók számára.

**Becsült óra:** ~32-48

---

## Phase 6 — Developer Experience (M6.1-M6.5)

> Senki nem vesz meg egy framework-öt, ha nem kellemes használni.

**Összesen: ~100-140 óra**

### M6.1 — CLI Eszközök

| Elem | Leírás |
|---|---|
| `symphact` CLI | `new`, `build`, `run`, `deploy`, `monitor` parancsok. |
| `dotnet new symphact-app` | Console actor app sablon. |
| `dotnet new symphact-service` | Supervised service sablon. |
| `dotnet new symphact-device` | Device actor sablon. |

**Becsült óra:** ~20-28

---

### M6.2 — IDE Integráció

| Elem | Leírás |
|---|---|
| VS Code extension | Actor state inspector, message trace viewer. |
| Rider plugin | JetBrains integráció. |
| Live actor tree | Supervision hierarchia vizualizáció. |
| Message breakpoint | Töréspontok message receive-en (nem csak kódsoron). |

**Becsült óra:** ~24-32

---

### M6.3 — Profiler & Monitoring Dashboard

| Elem | Leírás |
|---|---|
| Actor profiler | msg/sec per actor, latency histogram, mailbox depth. |
| System dashboard | Web UI: actor tree, message flow, resource usage. |
| Prometheus export | Metrikák exportja. |
| Grafana sablon | Kész dashboard template. |

**Becsült óra:** ~24-32

---

### M6.4 — NuGet Csomagok & API Reference

| Csomag | Tartalom |
|--------|----------|
| `Symphact.Core` | Runtime primitívek. |
| `Symphact.Supervision` | Supervisor stratégiák. |
| `Symphact.Persistence` | Event sourcing. |
| `Symphact.Remoting` | Distribution. |
| `Symphact.Security` | Capability modell. |
| `Symphact.Devices` | Device actor framework. |

API reference site (DocFX vagy hasonló).

**Becsült óra:** ~16-24

---

### M6.5 — Dokumentáció & Oktatóanyagok

| Elem | Leírás |
|---|---|
| Getting started | 30 perc alatt első actor app. |
| Architecture deep-dive | A vision doc fejlesztői verziója. |
| Migration guide | Akka.NET-ről / Orleans-ról való átállás. |
| Tutorial sorozat | IoT gateway, AI agent cluster, SNN demo. |
| FAQ + troubleshooting | Gyakori kérdések és megoldások. |

**Becsült óra:** ~16-24

---

## Phase 7 — Első Eladható Termék (M7.1-M7.5)

> A termék amire egy cég rábólintana: "ezt megveszem / erre fizetek support-ot."

**Összesen: ~80-120 óra**

### M7.1 — Referencia Alkalmazások

| Alkalmazás | Leírás |
|---|---|
| IoT Gateway | Sensor actors → protocol actors → cloud actor (MQTT/HTTP). |
| AI Agent Cluster | Supervisor hierarchy + capability-secured LLM agents. |
| SNN Demo | 1000 LIF neuron actor, real-time spike propagation. |
| Chat Server | Multi-room, multi-user, hot code update demo. |

**Becsült óra:** ~24-32

---

### M7.2 — Production Hardening

| Elem | Leírás |
|---|---|
| Stress testing | 10K+ actor, tartós terhelés. |
| Chaos testing | Véletlenszerű actor kill, hálózati partíció. |
| Memory leak detekció | Long-running soak test. |
| Performance CI | Benchmark minden PR-nél. |

**Becsült óra:** ~20-28

---

### M7.3 — Enterprise Feature-ök

| Elem | Leírás |
|---|---|
| RBAC | Role → capability set mapping. |
| Multi-tenant izoláció | Tenant = supervision subtree. |
| Konfiguráció kezelés | Actor-based config service. |
| Health check API | K8s liveness/readiness probe kompatibilis. |

**Becsült óra:** ~16-24

---

### M7.4 — Licencelés & Csomagolás

| Elem | Leírás |
|---|---|
| Dual license | Apache-2.0 (community) + Commercial (enterprise support). |
| Docker image | `symphactos-runtime`. |
| Helm chart | K8s deploy. |
| symphact.org landing page | Termék bemutató oldal. |

**Becsült óra:** ~8-12

---

### M7.5 — Launch Felkészülés

| Elem | Leírás |
|---|---|
| Announcement blog post | Nyilvános bemutató cikk. |
| Conference anyag | NDC, DotNext, Strange Loop előadás. |
| GitHub Sponsors / Open Collective | Közösségi finanszírozás. |
| Enterprise support SLA | Vállalati támogatási dokumentum. |
| Első partner onboarding | 1-2 early adopter cég bevonása. |

**Becsült óra:** ~12-16
