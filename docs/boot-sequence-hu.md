# Symphact Boot Sequence

> English version: [boot-sequence-en.md](boot-sequence-en.md)

> A Symphact indulási folyamata — a hardveres boot **után**, az első alkalmazás actor-ig.

> Version: 1.0

---

## Repo felelősségek

A boot folyamat **két repo** kódját érinti:

| Repo | Mit tartalmaz? |
|------|---------------|
| **[FenySoft/CLI-CPU](https://github.com/FenySoft/CLI-CPU)** | A chip RTL-je, Seal Core, MMIO regiszterek, HW mailbox FIFO, eFuse root hash. **A hardveres boot szekvenciát** (POR → Seal Core verify → Rich core start) lásd: [hw-boot-hu.md](https://github.com/FenySoft/CLI-CPU/blob/main/docs/hw-boot-hu.md) |
| **[FenySoft/Symphact](https://github.com/FenySoft/Symphact)** | Az OS kódja: `Boot.Main()`, actor runtime, kernel actor-ok, device actor-ok, alkalmazások |

**Szabály:** A Seal Core firmware a **hardver része** (mask ROM-ba égetve, a chip-pel szállítják). A Symphact a **szoftver** (flash-en, frissíthető, aláírással védve). Két repo, két életciklus, két licenc (CERN-OHL-S vs Apache-2.0).

---

## Előfeltétel: hardveres boot (1-3. lépés)

A Symphact indulása előtt a chip hardveresen elvégzi:

1. **Power-On Reset** — minden core reset
2. **Seal Core boot** — self-test, eFuse root hash olvasás, QSPI flash → SRAM másolás, SHA-256 + WOTS+/LMS HW verifikáció
3. **Rich core start** — Seal Core jelzi: verified code ready, Rich core indul a Quench-RAM CODE régióból

Részletes leírás: **[CLI-CPU/docs/hw-boot-hu.md](https://github.com/FenySoft/CLI-CPU/blob/main/docs/hw-boot-hu.md)**

A kriptográfiai modellt (PQC, WOTS+/LMS, trust chain, tanúsítvány formátum, HSM Card) lásd: **[CLI-CPU/docs/authcode-hu.md](https://github.com/FenySoft/CLI-CPU/blob/main/docs/authcode-hu.md)**

---

## Áttekintés — OS boot szekvencia

```
[HW boot kész — Rich core indul]
     │
     ▼
[4. Boot.Main()] ──────── MMIO discovery, core count, mailbox mapping
     │                     Kód: Symphact repo (src/Symphact.Boot/)
     ▼
[5. Root Supervisor] ──── Első actor, Admin capability
     │                     Mailbox FIFO enable
     ▼
[6. Kernel Actors] ────── Scheduler, Router, CapabilityRegistry, Supervisors
     ▼
[7. Device Actors] ────── UART, GPIO, Timer, Flash device actors
     ▼
[8. Nano Core-ok Wake] ── Wake interrupt, code loading Nano SRAM-ba
     │                     Scheduler dönt melyik core-ra melyik actor
     ▼
[9. App Supervisor] ───── Alkalmazás actor tree spawn
     ▼
[10. Első alkalmazás] ─── Alkalmazás actor-ok futnak Nano core-okon
     ▼
[11. Rendszer üzemkész] ── Minden core dolgozik, message-ek folynak
```

---

## 4. Symphact.Boot.Main() — rendszer inicializálás

| Szoftver | Hardver (MMIO) |
|----------|----------------|
| A Rich core futtatja `Symphact.Boot.Main()`-t. | A Rich core folyamatosan CIL-t hajt végre. |
| **Innentől a Symphact repo kódja fut.** | A Seal Core verifikálta — a kód hiteles. |
| | |
| **4a. GC inicializálás** | A Rich core SRAM heap régiójában **bump allocator** indul. |
| `THeapManager.Init(heapStart, heapEnd)` | Mark-sweep GC később, amikor a heap megtelik. |
| | |
| **4b. Core discovery** | A Rich core olvassa a **core count register-t** (MMIO `0xF0000100`). |
| `var ANanoCoreCount = Mmio.Read<int>(0xF0000100)` | Visszaadja: pl. 10 000 (Nano) + 1 (Rich) = 10 001. |
| `var ARichCoreCount = Mmio.Read<int>(0xF0000104)` | Két külön register: Nano count és Rich count. |
| | |
| **4c. Mailbox mapping** | A Rich core olvassa a **mailbox base address register-t** (MMIO `0xF0000200`). |
| Minden core mailbox FIFO-jának fizikai címét kiszámolja: | Mailbox address = base + core_id × FIFO_SIZE. |
| `base + core_id * FIFO_SLOT_SIZE` | Pl. FIFO_SLOT_SIZE = 64 byte (8 slot × 8 byte/slot). |
| | |
| **4d. Interrupt vektor beállítás** | A Rich core interrupt controller-e MMIO-n konfigurálható (MMIO `0xF0000600`). |
| Beállítja melyik interrupt → melyik handler: | Interrupt sources: mailbox not-empty, watchdog, trap from Nano core. |
| `Mmio.Write(0xF0000600, MAILBOX_IRQ_HANDLER_ADDR)` | |

**Forráskód helye:** `FenySoft/Symphact` repo — `src/Symphact.Boot/TBoot.cs`

MMIO regiszterek részletes leírása: [CLI-CPU/docs/hw-boot-hu.md §MMIO](https://github.com/FenySoft/CLI-CPU/blob/main/docs/hw-boot-hu.md#mmio)

---

## 5. Root Supervisor actor elindul

| Szoftver | Hardver |
|----------|---------|
| `Boot.Main()` létrehozza az **első actor-t**: `TRootSupervisor`. | A Rich core saját SRAM-jában jön létre az actor state. |
| | Nincs core váltás — a root supervisor a Rich core-on fut. |
| `TRootSupervisor.Init()` visszaad: | |
| - üres child lista | |
| - restart stratégia = "mindig újraindít" | |
| - max restart count = végtelen | |
| | |
| A Rich core **saját mailbox FIFO-ja aktiválódik**: | MMIO write: `Mmio.Write(0xF0000300 + rich_core_id * 4, MAILBOX_ENABLE)` |
| Innentől a root supervisor üzeneteket fogadhat. | A HW mailbox FIFO aktív — bejövő üzenetek interrupt-ot generálnak. |
| | |
| A root supervisor **TActorRef capability-t kap**: | A capability a boot trust-ből származik. |
| `[core-id: Rich][offset: 0][perms: Admin]` | A `TCapabilityRegistry` (6d lépés) fogja kezelni a runtime capability-ket. |
| | |
| Ez az **egyetlen** actor akinek Admin capability-je van. | |
| Minden más actor **ettől kap jogot** (capability delegation). | |

**Forráskód helye:** `FenySoft/Symphact` repo — `src/Symphact.Core/TRootSupervisor.cs` (M2.1 milestone)

**Mi az Admin capability?** A root supervisor korlátlan jogokkal rendelkezik:
- Spawn bármilyen actor-t
- Kill bármilyen actor-t
- Delegálhat bármilyen permission-t
- Hozzáfér minden device actor-hoz

**Miért csak egy Admin van?** Biztonsági okokból — a capability model lényege, hogy a jogok delegálással terjednek, nem globális hozzáféréssel. A root supervisor az egyetlen "trust anchor" a runtime-ban (az eFuse root hash a HW trust anchor).

---

## 6. Kernel aktorok spawn-olódnak

| Szoftver | Hardver |
|----------|---------|
| A root supervisor spawn-olja a **kernel actor-okat** sorban: | |
| | |
| **6a.** `TKernelCoreSupervisor` | Még mindig a Rich core-on fut minden. |
| Felügyeli a kernel actor-okat (scheduler, router, registry). | A Nano core-ok **még alszanak**. |
| OneForOne stratégia — ha egy kernel actor crash-el, csak azt indítja újra. | |
| | |
| **6b.** `TScheduler` | A scheduler olvassa a **core status register-eket**: |
| Actor-to-core hozzárendelés, load balancing. | `Mmio.Read(0xF0000400 + core_id * 4)` → Sleeping / Running / Error |
| Nyilvántartja: melyik core szabad, melyik mit futtat. | Egyelőre minden Nano core "Sleeping" státuszban van. |
| | |
| **6c.** `TRouter` | A router olvassa a **mailbox address table-t**: |
| Logikai ref → fizikai cím feloldás. | `Mmio.Read(0xF0000800 + core_id * 4)` → mailbox FIFO fizikai cím |
| Cache-eli a mapping-eket a gyors lookup-hoz. | Ez a HW → SW "telefonkönyv": core-id → mailbox FIFO cím. |
| | |
| **6d.** `TCapabilityRegistry` | Runtime capability kezelés. |
| Capability-k kiadása, delegálás, visszavonás. | A boot trust-ből származó jogot delegálja tovább. |
| Nyilvántartja az összes kiadott capability-t. | F6+: HW SHA-256 accelerator gyorsítja a hash compute-ot. |
| Visszavonáskor broadcast: érintett router-ek frissítik a cache-t. | |
| | |
| **6e.** `TKernelIoSupervisor` | Még **nem** spawn-ol device actor-okat. |
| A device actor-ok felügyelete (UART, GPIO, Timer, Flash). | Ez a következő lépés (7.). |

**Spawn sorrend miért fontos?**
1. A `TKernelCoreSupervisor` kell először — ő felügyeli a többit
2. A `TScheduler` kell a `TRouter` előtt — a router-nek tudnia kell melyik core szabad
3. A `TCapabilityRegistry` kell a device actor-ok előtt — azoknak capability kell
4. A `TKernelIoSupervisor` utolsó — ő spawn-olja a device actor-okat a 7. lépésben

**Forráskód helye:** `FenySoft/Symphact` repo — `src/Symphact.Core/` (M0.3 supervision + M2.1-M2.5 kernel actors)

**Állapot a 6. lépés után:**
- Rich core-on fut **7 kernel actor**: root + 2 supervisor + scheduler + router + registry + io_sup
- A Seal Core **heartbeat-et küld** a health monitor-nak (redundancia)
- A Rich core **time-sliced**: a kernel actor-ok cooperative scheduling-gel osztoznak az egyetlen core-on
- A Nano core-ok **még alszanak** — a következő lépésben kelnek fel
- Az actor tree:

```
TRootSupervisor [Admin]
├── TKernelCoreSupervisor
│   ├── TScheduler
│   ├── TRouter
│   └── TCapabilityRegistry
└── TKernelIoSupervisor
    └── (üres — device actor-ok a 7. lépésben)
```

---

## 7. Device aktorok spawn-olódnak

*(következő szakasz — kérdés esetén folytatjuk)*

---

## Runtime Actor Loading (hot code)

A boot-on kívül **runtime-ban is tölthetők** új actor-ok. A `THotCodeLoader` kernel actor végzi:

```
Developer:
  1. C# kód → CIL bytecode (dotnet build)
  2. SHA-256(bytecode) kiszámítás
  3. Aláírás HSM Card-dal

CFPU (THotCodeLoader actor):
  4. SHA-256(bytecode) == cert hash?         → code-hash check
  5. Cert-chain verify → eFuse root?         → chain check
  6. Revocation check (cert visszavonva?)     → revocation check
  7. Mind OK → actor spawn                   → VAGY: BLOCK, nem tölt be
```

Az aláírási modell, tanúsítvány formátum és HSM Card részletei: [CLI-CPU/docs/authcode-hu.md](https://github.com/FenySoft/CLI-CPU/blob/main/docs/authcode-hu.md)

---

## .NET Reference Implementáció

A boot sequence fenti leírása a **CFPU hardverre** vonatkozik. A .NET reference runtime (`src/Symphact.Core/`) **szimulálva** futtatja ugyanezt:

| Boot lépés | CFPU HW-en | .NET Reference Impl-ben |
|-----------|------------|------------------------|
| 1-3. HW boot | POR → Seal Core → Rich core | Nincs (a .NET CLR kezeli) |
| 4. Boot.Main() | MMIO register olvasás | Constructor: core count = `Environment.ProcessorCount` |
| 5. Root Supervisor | Első actor a Rich core-on | `system.Spawn<TRootSupervisor>()` |
| 6. Kernel actors | Rich core time-sliced | `system.Spawn<TScheduler>()`, stb. |
| 7. Device actors | MMIO periféria actor-ok | Mock device actor-ok (szimulált UART, GPIO) |
| 8. Nano wake | HW wake interrupt | `Task.Run()` — thread per "core" |
| 9-11. App actors | Nano core-okon futnak | Thread pool-on futnak |

**Ez a reference impl célja:** igazolni, hogy az actor modell működik, a supervision helyes, a message routing korrekt — **mielőtt szilícium lenne**.

---

## Changelog

| Verzió | Dátum | Összefoglaló |
|--------|-------|-------------|
| 1.0 | 2026-04-18 | Kezdeti kiadás |
