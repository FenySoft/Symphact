# Symphact Boot Sequence

> Magyar verzió: [boot-sequence-hu.md](boot-sequence-hu.md)

> The Symphact startup process — **after** the hardware boot, up to the first application actor.

> Version: 1.0

---

## Repo Responsibilities

The boot process involves code from **two repos**:

| Repo | What does it contain? |
|------|----------------------|
| **[FenySoft/CLI-CPU](https://github.com/FenySoft/CLI-CPU)** | The chip RTL, Seal Core, MMIO registers, HW mailbox FIFO, eFuse root hash. **The hardware boot sequence** (POR → Seal Core verify → Rich core start) is described in: [hw-boot-hu.md](https://github.com/FenySoft/CLI-CPU/blob/main/docs/hw-boot-hu.md) |
| **[FenySoft/Symphact](https://github.com/FenySoft/Symphact)** | The OS code: `Boot.Main()`, actor runtime, kernel actors, device actors, applications |

**Rule:** The Seal Core firmware is **part of the hardware** (burned into mask ROM, shipped with the chip). Symphact is **software** (stored on flash, updatable, protected by signatures). Two repos, two lifecycles, two licenses (CERN-OHL-S vs Apache-2.0).

---

## Prerequisite: Hardware Boot (Steps 1-3)

Before Symphact starts, the chip completes the hardware boot:

1. **Power-On Reset** — all cores reset
2. **Seal Core boot** — self-test, eFuse root hash read, QSPI flash → SRAM copy, SHA-256 + WOTS+/LMS HW verification
3. **Rich core start** — Seal Core signals: verified code ready, Rich core starts from the Quench-RAM CODE region

Detailed description: **[CLI-CPU/docs/hw-boot-hu.md](https://github.com/FenySoft/CLI-CPU/blob/main/docs/hw-boot-hu.md)**

The cryptographic model (PQC, WOTS+/LMS, trust chain, certificate format, HSM Card) is described in: **[CLI-CPU/docs/authcode-hu.md](https://github.com/FenySoft/CLI-CPU/blob/main/docs/authcode-hu.md)**

---

## Overview — OS Boot Sequence

```
[HW boot complete — Rich core starts]
     │
     ▼
[4. Boot.Main()] ──────── MMIO discovery, core count, mailbox mapping
     │                     Code: Symphact repo (src/Symphact.Boot/)
     ▼
[5. Root Supervisor] ──── First actor, Admin capability
     │                     Mailbox FIFO enable
     ▼
[6. Kernel Actors] ────── Scheduler, Router, CapabilityRegistry, Supervisors
     ▼
[7. Device Actors] ────── UART, GPIO, Timer, Flash device actors
     ▼
[8. Nano Core Wake] ──── Wake interrupt, code loading into Nano SRAM
     │                     Scheduler decides which actor goes to which core
     ▼
[9. App Supervisor] ───── Application actor tree spawn
     ▼
[10. First Application] ── Application actors running on Nano cores
     ▼
[11. System Ready] ─────── All cores working, messages flowing
```

---

## 4. Symphact.Boot.Main() — System Initialization

| Software | Hardware (MMIO) |
|----------|----------------|
| The Rich core runs `Symphact.Boot.Main()`. | The Rich core continuously executes CIL. |
| **From this point, Symphact repo code is running.** | The Seal Core verified it — the code is authentic. |
| | |
| **4a. GC initialization** | A **bump allocator** starts in the Rich core's SRAM heap region. |
| `THeapManager.Init(heapStart, heapEnd)` | Mark-sweep GC later, when the heap fills up. |
| | |
| **4b. Core discovery** | The Rich core reads the **core count register** (MMIO `0xF0000100`). |
| `var ANanoCoreCount = Mmio.Read<int>(0xF0000100)` | Returns: e.g. 10,000 (Nano) + 1 (Rich) = 10,001. |
| `var ARichCoreCount = Mmio.Read<int>(0xF0000104)` | Two separate registers: Nano count and Rich count. |
| | |
| **4c. Mailbox mapping** | The Rich core reads the **mailbox base address register** (MMIO `0xF0000200`). |
| Calculates the physical address of each core's mailbox FIFO: | Mailbox address = base + core_id × FIFO_SIZE. |
| `base + core_id * FIFO_SLOT_SIZE` | E.g. FIFO_SLOT_SIZE = 64 bytes (8 slots × 8 bytes/slot). |
| | |
| **4d. Interrupt vector setup** | The Rich core's interrupt controller is configurable via MMIO (MMIO `0xF0000600`). |
| Sets up which interrupt → which handler: | Interrupt sources: mailbox not-empty, watchdog, trap from Nano core. |
| `Mmio.Write(0xF0000600, MAILBOX_IRQ_HANDLER_ADDR)` | |

**Source code location:** `FenySoft/Symphact` repo — `src/Symphact.Boot/TBoot.cs`

MMIO register details: [CLI-CPU/docs/hw-boot-hu.md §MMIO](https://github.com/FenySoft/CLI-CPU/blob/main/docs/hw-boot-hu.md#mmio)

---

## 5. Root Supervisor Actor Starts

| Software | Hardware |
|----------|---------|
| `Boot.Main()` creates the **first actor**: `TRootSupervisor`. | The actor state is created in the Rich core's own SRAM. |
| | No core switch — the root supervisor runs on the Rich core. |
| `TRootSupervisor.Init()` returns: | |
| - empty child list | |
| - restart strategy = "always restart" | |
| - max restart count = infinite | |
| | |
| The Rich core's **own mailbox FIFO is activated**: | MMIO write: `Mmio.Write(0xF0000300 + rich_core_id * 4, MAILBOX_ENABLE)` |
| From now on, the root supervisor can receive messages. | The HW mailbox FIFO is active — incoming messages generate interrupts. |
| | |
| The root supervisor **receives a TActorRef capability**: | The capability derives from the boot trust. |
| `[core-id: Rich][offset: 0][perms: Admin]` | The `TCapabilityRegistry` (step 6d) will manage runtime capabilities. |
| | |
| This is the **only** actor with Admin capability. | |
| Every other actor **receives rights from this one** (capability delegation). | |

**Source code location:** `FenySoft/Symphact` repo — `src/Symphact.Core/TRootSupervisor.cs` (M2.1 milestone)

**What is Admin capability?** The root supervisor has unrestricted rights:
- Spawn any actor
- Kill any actor
- Delegate any permission
- Access every device actor

**Why only one Admin?** For security reasons — the essence of the capability model is that rights propagate through delegation, not global access. The root supervisor is the only "trust anchor" at runtime (the eFuse root hash is the HW trust anchor).

---

## 6. Kernel Actors Are Spawned

| Software | Hardware |
|----------|---------|
| The root supervisor spawns **kernel actors** in order: | |
| | |
| **6a.** `TKernelCoreSupervisor` | Everything still runs on the Rich core. |
| Supervises kernel actors (scheduler, router, registry). | The Nano cores are **still asleep**. |
| OneForOne strategy — if a kernel actor crashes, only that one is restarted. | |
| | |
| **6b.** `TScheduler` | The scheduler reads **core status registers**: |
| Actor-to-core assignment, load balancing. | `Mmio.Read(0xF0000400 + core_id * 4)` → Sleeping / Running / Error |
| Tracks: which core is free, which is running what. | For now, all Nano cores are in "Sleeping" status. |
| | |
| **6c.** `TRouter` | The router reads the **mailbox address table**: |
| Logical ref → physical address resolution. | `Mmio.Read(0xF0000800 + core_id * 4)` → mailbox FIFO physical address |
| Caches the mappings for fast lookup. | This is the HW → SW "phone book": core-id → mailbox FIFO address. |
| | |
| **6d.** `TCapabilityRegistry` | Runtime capability management. |
| Capability issuance, delegation, revocation. | Delegates the right derived from boot trust. |
| Tracks all issued capabilities. | F6+: HW SHA-256 accelerator speeds up hash computation. |
| On revocation, broadcast: affected routers update their cache. | |
| | |
| **6e.** `TKernelIoSupervisor` | Does **not** spawn device actors yet. |
| Supervises device actors (UART, GPIO, Timer, Flash). | That is the next step (7.). |

**Why does spawn order matter?**
1. `TKernelCoreSupervisor` must come first — it supervises the rest
2. `TScheduler` must come before `TRouter` — the router needs to know which core is free
3. `TCapabilityRegistry` must come before device actors — they need capabilities
4. `TKernelIoSupervisor` is last — it spawns device actors in step 7

**Source code location:** `FenySoft/Symphact` repo — `src/Symphact.Core/` (M0.3 supervision + M2.1-M2.5 kernel actors)

**State after step 6:**
- The Rich core runs **7 kernel actors**: root + 2 supervisors + scheduler + router + registry + io_sup
- The Seal Core **sends heartbeats** to the health monitor (redundancy)
- The Rich core is **time-sliced**: kernel actors share the single core via cooperative scheduling
- The Nano cores are **still asleep** — they wake up in the next step
- The actor tree:

```
TRootSupervisor [Admin]
├── TKernelCoreSupervisor
│   ├── TScheduler
│   ├── TRouter
│   └── TCapabilityRegistry
└── TKernelIoSupervisor
    └── (empty — device actors in step 7)
```

---

## 7. Device Actors Are Spawned

*(next section — to be continued if requested)*

---

## Runtime Actor Loading (hot code)

Beyond boot, **new actors can be loaded at runtime**. The `THotCodeLoader` kernel actor handles this:

```
Developer:
  1. C# code → CIL bytecode (dotnet build)
  2. SHA-256(bytecode) computation
  3. Signing with HSM Card

CFPU (THotCodeLoader actor):
  4. SHA-256(bytecode) == cert hash?         → code-hash check
  5. Cert-chain verify → eFuse root?         → chain check
  6. Revocation check (cert revoked?)        → revocation check
  7. All OK → actor spawn                   → OR: BLOCK, do not load
```

The signing model, certificate format and HSM Card details are described in: [CLI-CPU/docs/authcode-hu.md](https://github.com/FenySoft/CLI-CPU/blob/main/docs/authcode-hu.md)

---

## .NET Reference Implementation

The boot sequence described above refers to the **CFPU hardware**. The .NET reference runtime (`src/Symphact.Core/`) runs the same process **in simulation**:

| Boot Step | On CFPU HW | In .NET Reference Impl |
|-----------|------------|------------------------|
| 1-3. HW boot | POR → Seal Core → Rich core | N/A (handled by .NET CLR) |
| 4. Boot.Main() | MMIO register read | Constructor: core count = `Environment.ProcessorCount` |
| 5. Root Supervisor | First actor on Rich core | `system.Spawn<TRootSupervisor>()` |
| 6. Kernel actors | Rich core time-sliced | `system.Spawn<TScheduler>()`, etc. |
| 7. Device actors | MMIO peripheral actors | Mock device actors (simulated UART, GPIO) |
| 8. Nano wake | HW wake interrupt | `Task.Run()` — thread per "core" |
| 9-11. App actors | Running on Nano cores | Running on thread pool |

**Purpose of this reference impl:** to verify that the actor model works, supervision is correct, message routing is accurate — **before silicon exists**.

---

## Changelog

| Version | Date | Summary |
|---------|------|---------|
| 1.0 | 2026-04-18 | Initial release |
