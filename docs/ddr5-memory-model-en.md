# DDR5 Memory Model — Symphact Perspective

> Magyar verzió: [ddr5-memory-model-hu.md](ddr5-memory-model-hu.md)

> Version: 1.2

This document captures the Symphact **DDR5 memory management model**: how an actor requests access, uses memory, and releases it. It documents not only the final result, but the **reasoning path** as well.

> **Target audience:** OS developers, Symphact API designers, actor-software developers. The hardware (RTL) perspective is covered in [CLI-CPU docs/ddr5-architecture-hu.md](https://github.com/FenySoft/CLI-CPU/blob/main/docs/ddr5-architecture-hu.md).

## Context: No Kernel, No Kernel/User Mode

In Symphact, there is **no traditional kernel layer**. Hardware isolation (shared-nothing multi-core, per-core SRAM) already guarantees what other OSes achieve with kernel/user mode switching. Instead, **privilege levels are expressed through actor relationships**:

```
         root_supervisor
        /               \
kernel_core_sup      kernel_io_sup
   /    \               /      \
scheduler allocator  ddr5_gw   eth_gw   ...
                        |
                   DDR5 Controller (HW, config port)
```

The `kernel_io_sup` and its children are **actors just like any application actor** — they run on Rich Core, communicate via mailbox. The difference: through the **capability** received from the `root_supervisor`, they know the DDR5 Controller config port MMIO address.

---

## Decision 1: Who Initiates DDR5 Access?

### 1.a) Rejected approach: kernel_io_sup schedules the loading

The first idea was that `kernel_io_sup` (OS) decides when and what data to load into the core's SRAM — via centralized DMA scheduling.

**Why we rejected it:** The OS cannot know when and what data the actor needs. Only the actor itself knows its own processing logic. Centralized scheduling adds **unnecessary complexity** and **latency** to the system.

### 1.b) Rejected approach: the actor directly accesses DDR5

The other extreme: the actor freely reads DDR5 via MMIO, without any permission request.

**Why we rejected it:** If any actor can read any DDR5 address, the shared-nothing isolation is an **illusion**. A compromised actor could read other actors' data.

### 1.c) Final decision: the actor requests, kernel_io_sup authorizes

The actor **initiates** the access itself, but `kernel_io_sup` **verifies and authorizes** it. This is the capability model:

- The actor knows what it needs → it requests
- `kernel_io_sup` knows what it's allowed → it decides
- The DDR5 Controller CAM table validates → HW guarantees

---

## Decision 2: Per-Request or One-Time Grant for Access?

### 2.a) Rejected approach: a message for every DDR5 operation

The actor sends a message to `kernel_io_sup` before every read/write, which performs the operation and sends back the result.

**Why we rejected it:** Every single DDR5 access would require three messages (request → io_sup → DDR5 → io_sup → response). This triples the latency. If an actor works intensively with a DDR5 region (e.g., large table scan), this is unacceptable overhead.

### 2.b) Final decision: capability grant — one-time authorization, free use

The actor requests access once, receives the capability (region + permissions), and then **directly reads/writes DDR5 via MMIO**:

```
1. Actor --> kernel_io_sup: MsgGrantRequest(ObjectId, Access: RW)
2. kernel_io_sup verifies the permission
3. kernel_io_sup --> DDR5 Controller config port: add CAM entry
4. kernel_io_sup --> Actor: MsgGranted(Region: start, length, access)
5. Actor freely reads/writes via MMIO
   ... as many times as needed, without further authorization
6. Actor --> kernel_io_sup: MsgReleaseRegion(Region)
7. kernel_io_sup --> DDR5 Controller config port: delete CAM entry
```

**Decision rationale:**
- One-time authorization request = one-time latency, then zero overhead
- The HW CAM table ensures the core can only access its own region
- If the actor crashes, the supervisor notifies `kernel_io_sup` → capability is automatically revoked

---

## Decision 3: Ownership Model

### 3.a) Rejected approach: simultaneous access by multiple actors

We could allow multiple actors to simultaneously read the same DDR5 region (reader-writer lock or immutable shared data analogy).

**Why we rejected it:** Even read-only sharing is **shared state** — exactly what the shared-nothing architecture excludes. If multiple cores see a region simultaneously, an implicit coupling is created between them. If a core needs another core's data, **it receives a copy via message** — this is the fundamental principle of the actor model.

### 3.b) Final decision: single owner, always

A DDR5 region can be accessed by **only one core at a time**, whether for reading or writing. If Core 42 received it, no other core can get it until Core 42 has released it.

If another core also needs the same data, there are two solutions:

```
1. Copy via message:
   Core 42 (owner) --> MsgData(content) --> Core 99

2. Sequential access:
   Core 42 receives → processes → releases (MsgReleaseRegion)
   Core 99 receives → processes → releases
```

---

## Actor API — The Programmer's Perspective

The programmer **thinks in objects**, not DDR5 regions, addresses, or MMIO operations. The API mirrors C#'s natural data handling approach: load objects, modify them, and save — the fact that DDR5, CAM tables, or NoC flits are involved in the background is **invisible**.

> **IMPORTANT:** In the actor model there is **no `await`** — await causes deadlock because it blocks mailbox processing. Every operation is message-send + response in a separate `Handle` method.

```csharp
public class TOrderProcessor : CfpuActor
{
    public void Handle(MsgProcessOrder AMsg)
    {
        // I request an object — I know nothing about DDR5, MMIO, CAM tables
        FStoreRef.Send(new MsgLoad<TCustomer>(AMsg.CustomerId));
    }

    public void Handle(MsgLoaded<TCustomer> AMsg)
    {
        // The object is in the core's SRAM — I can work with it
        var LCustomer = AMsg.Object;
        LCustomer.Balance += 100;

        // Save it back — the Store actor handles DDR5 details
        FStoreRef.Send(new MsgSave<TCustomer>(LCustomer));
    }

    public void Handle(MsgSaved AMsg)
    {
        // Object written, DDR5 capability released
        FRequesterRef.Send(new MsgOrderDone());
    }

    public void Handle(MsgLoadFailed AMsg)
    {
        // Failed (busy, does not exist, no permission)
    }
}
```

### What Happens Behind the Scenes?

`FStoreRef` is a **Store actor** (child of `kernel_io_sup`) that hides DDR5 management from the programmer:

```
Programmer:     MsgLoad<TCustomer>(id)
                    |
Store actor:    ObjectId → DDR5 address mapping
                MsgGrantRequest → kernel_io_sup
                DDR5 read (MMIO)
                Deserialization → TCustomer object
                MsgLoaded<TCustomer> → requesting actor
                MsgReleaseRegion → kernel_io_sup
```

The programmer **sends one message** (`MsgLoad`) and **receives one response** (`MsgLoaded` + the object). The DDR5 capability grant/release, serialization, and memory management are the Store actor's internal concern.

## Message Types

### Programmer Level (what the developer sees)

| Message | Direction | Description |
|---------|-----------|-------------|
| `MsgLoad<T>(ObjectId)` | Actor → Store | Load an object |
| `MsgLoaded<T>(Object)` | Store → Actor | Object loaded, in SRAM |
| `MsgSave<T>(Object)` | Actor → Store | Save an object |
| `MsgSaved(ObjectId)` | Store → Actor | Save complete |
| `MsgLoadFailed(Reason)` | Store → Actor | Load failed |

### System Level (used internally by the Store actor)

| Message | Direction | Description |
|---------|-----------|-------------|
| `MsgGrantRequest(ObjectId, Access)` | Store → kernel_io_sup | Request DDR5 access |
| `MsgGranted(Region)` | kernel_io_sup → Store | Access granted |
| `MsgReleaseRegion(Region)` | Store → kernel_io_sup | Release access |

## Crash Recovery

If an actor crashes and has not released its capability:

```
1. Actor generates a trap
2. The core HW detects it (cooperative switching level)
3. Scheduler notifies the supervisor: MsgActorCrashed(ActorId)
4. Supervisor signals kernel_io_sup: MsgActorCrashed(src[24], src_actor[8])
5. kernel_io_sup deletes ONLY that actor's CAM entries
6. Other actors on the same core continue running undisturbed
7. The region is freed — another actor can receive it
```

This is the hardware implementation of Erlang/OTP's "let it crash" model — the actor does not need to "clean up after itself", the supervisor hierarchy handles it. Due to N:M actor-to-core mapping, crash recovery is **actor-level**, not core-level — the DDR5 Controller CAM table identifies entries based on `src[24] + src_actor[8]` (see `interconnect-hu.md` v2.4 header spec).

## Related HW Decisions

Symphact developers should be aware of the HW constraints that influence the API:

| HW Fact | OS Consequence |
|---------|----------------|
| The DDR5 Controller has **10 ports** | Max ~10 simultaneous DDR5 request processing — but cores rarely access DDR5 (they work from SRAM) |
| The CAM table has a **finite size** | kernel_io_sup must track active capabilities and limit the number of simultaneous grants |
| The CAM checks based on `src[24] + src_actor[8]` | **Actor-level** authorization — multiple actors on one core can receive separate DDR5 regions (max 256 actors/core) |
| **src[24] is unforgeable** (HW) | The OS does not need to separately identify the core — the HW has already done it |
| **src_actor[8] is filled by the core HW** (active actor context register), unforgeable | The actor cannot overwrite its own actor ID — the HW automatically fills it from the current context register |
| The config port is **hardwired** | kernel_io_sup must run on the Rich Core that is physically connected to the config port |

## Changelog

| Version | Date | Summary |
|---------|------|---------|
| 1.2 | 2026-04-24 | src_actor[16] → src_actor[8] (max 256 actors/core). src_actor filled by: core HW (active actor context register), unforgeable. Core scheduler → Core HW fix |
| 1.1 | 2026-04-22 | Crash recovery fixed to actor-level (N:M mapping). CAM table src[24]+src_actor[16] based. Object-level actor API (MsgLoad/MsgSave, no await). HW decisions table extended |
| 1.0 | 2026-04-22 | First version — capability model, ownership, crash recovery, actor API |
