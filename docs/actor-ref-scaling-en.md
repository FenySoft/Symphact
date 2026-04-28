# Actor Reference Scaling and Defense Model — Symphact

> Magyar verzió: [actor-ref-scaling-hu.md](actor-ref-scaling-hu.md)

> Version: 3.0

> Status: finalized specification, basis for M0.6 / M0.7 / M2.5

This document records the **finalized bit layout**, **wire format**, and **defense-in-depth pyramid** of `TActorRef`. Version 3.0 is a **MAJOR change**: the previous HMAC-based model is replaced by the **CST (Capability Slot Table)** model — software never sees raw dst/src addresses, only an opaque CST index.

> **Audience:** Symphact runtime developers, CFPU HW designers (sister repo: `FenySoft/CLI-CPU`), reviewers of the API contract, security auditors.

---

## Context

`TActorRef` is the **cornerstone** of the Symphact public API: every actor-to-actor communication flows through it. `CLAUDE.md` records the API contract:

```csharp
public readonly record struct TActorRef(int SlotIndex);   // 32 bit, opaque CST index, public
```

The 32 bits are a **CST (Capability Slot Table) index** — opaque to software, resolved by HW at runtime to actual dst/src addresses. The user does not reach into the runtime's internal representation — the `int` is an opaque token, relevant only as `Equals`/`GetHashCode`/Send-parameter.

The previous spec (v2.0) prescribed a 64-bit `long ActorId` with `[HMAC:24][perms:8][actor-id:8][core-coord:24]` layout. Version 3.0 **eliminates the HMAC model** and the software-visible bit layout: the CST HW table holds the destination address, actor ID, and permissions.

---

## Threat model

The Symphact defense model focuses on **software + supply-chain** attacks. Physical-layer attacks are **out-of-scope** for this defense layer.

| Attack vector | Defense | Scope |
|---|---|---|
| Software permission forgery (fake CST index) | CST HW lookup + SEAL protection (invalid index = trap) | **In-scope** |
| Another actor reads an actor's CST table | Shared-nothing per-core QSRAM isolation + SEAL | **In-scope** |
| Compromised signer (malicious software) | AuthCode + supply-chain quarantine | **In-scope** |
| Permission escalation (perms manipulation) | Perms in CST, HW-only write, software cannot modify | **In-scope** |
| Cold boot (post-power-off freezing) | Quench-RAM RELEASE atomic wipe (side effect) | **In-scope** |
| **Bus-MITM, chip-decap, FIB probing** | — | **Out-of-scope** (physical access — stealing the SSD is easier) |
| **Side-channel (timing, power)** | — | **Out-of-scope** at base (Secure Edition F6.5 covers it) |

This is the Linux/Windows/macOS commercial threat model, and only the Secure Element (F6.5) extends to physical defense. Production deployments must explicitly document this (see [`trust-model-en.md`](trust-model-en.md)).

> **seL4 analogy:** The CST model is conceptually similar to seL4's capability-based security system — software receives capability handles (slot indices) that are resolved through a HW/kernel-level capability table. Software cannot see or modify the actual permission entries.

---

## The scaling challenge

CFPU chip core-count reference (`CLI-CPU/docs/core-types-hu.md`, 18 tine die @ 5nm, 1494 mm²):

| Core type | Node size | Cores / package | Bit requirement |
|---|---|---|---|
| Nano | 0.017 mm² | **~87,900** | 17 bit |
| Actor | 0.032 mm² | ~46,700 | 16 bit |
| Rich | 0.071 mm² | ~21,000 | 15 bit |

Scaling projection (`CLI-CPU/docs/chiplet-packaging-hu.md`):
- 2030 (2nm, 3D SRAM, 8 chiplets): 131,072 cores/package → 17 bit
- 2033+ (1.4nm): 262,144 cores/package → 18 bit
- F-9+ extrapolation (1–10M): 20–24 bit

The **24-bit core-coord** (16M cores) in the header is sufficient through F-9 generations — exactly matching the CLI-CPU `architecture-hu.md:1320` 24-bit HW address. The software-side `TActorRef` **does not contain this directly** — the CST HW table resolves it.

---

## Decision 1: `TActorRef` is 32-bit CST index

### 1.a) Rejected: 64-bit `long` HMAC-based ref (v2.0)

The previous `TActorRef(long ActorId)` encoded all information in 64 bits: `[HMAC:24][perms:8][actor-id:8][core-coord:24]`. This exposed HW address and permission bits to software.

**Why rejected:**
- Software-visible perms/HMAC bits represent an **attack surface**
- HMAC cryptographic verify is **area-expensive** (~5k gate / verify unit) and **latency-costly** (~10 cycles)
- The CST model replaces it with 1-cycle HW lookup, no cryptographic overhead

### 1.b) Rejected: 160-bit struct (earlier `vision-en.md`)

A 4-field struct (CoreId int + MailboxIndex int + CapabilityTag long + Permissions int) pushes complexity to the user, and increases the HW interconnect header size.

**Why rejected:**
- A capability must be an **opaque token** — the developer should not know the contents
- 160-bit verbatim transit on the tree fabric adds ~5–10% area

### 1.c) Decision: `TActorRef(int SlotIndex)` — 32-bit, opaque CST index

```csharp
public readonly record struct TActorRef(int SlotIndex);   // 32 bit, opaque
```

The `SlotIndex` points to a per-core CST (Capability Slot Table) entry. Software:
- **Cannot see** the destination core address, actor ID, or permissions
- **Cannot modify** the CST entry (HW-only write, SEAL-protected QSRAM)
- Can only issue **Send(ref, msg)** calls — HW lookup resolves CST → header mapping

`TActorRef` is chip-local — cross-chip addressing uses the proxy-actor pattern (see Decision 3).

---

## Decision 2: CST (Capability Slot Table) model

The previous v2.0 solution (`[HMAC:24][perms:8][actor-id:8][core-coord:24]` software bit layout) is **obsolete**. The HW-managed CST model replaces it.

### 2.a) Rejected: software HMAC-based bit layout (v2.0)

`TActorRef` carried all information in 64 bits. **Problems:**
- Required cryptographic defense against software brute-force (SipHash-128, area/latency overhead)
- `perms` bit-flags in the ref → software-manipulable (even though HW also checked)
- `core-coord` in the ref → software sees HW topology information (NOT opaque)

### 2.b) Decision: HW-managed CST in QSRAM, SEAL-protected

The CST is a per-core QSRAM table managed by hardware. Each entry is 8 bytes, aligned:

```
 63                                40 39    32 31    24 23          0
┌──────────────────────────────────────┬────────┬────────┬──────────┐
│            reserved                  │ perms  │actor-id│   dst    │
│             24 bit                   │  8 bit │  8 bit │  24 bit  │
└──────────────────────────────────────┴────────┴────────┴──────────┘
                                                          Σ = 64 bit (8 bytes)
```

| Field | Width | Rationale |
|---|---|---|
| `dst` | 24 bit | Destination core HW address. = CLI-CPU `dst[24]` in interconnect header. 16M cores → sufficient through F-9+ generations |
| `actor-id` | 8 bit | Destination actor ID. 256 actors/core — typical core hosts 1–100 actors (SRAM-bound) |
| `perms` | 8 bit, **bit-flags** | Send / Watch / Stop / Delegate / DelegateOnce / Migrate / MaxPri[2] |
| `reserved` | 24 bit | Reserved for future expansion (zeroed) |

**CST characteristics:**
- **QSRAM storage**: the CST lives in SEAL-protected Quench-RAM, cold-boot protected
- **HW-only write**: software cannot write to the CST directly — only the supervisor kernel can request CST entry creation/deletion via HW trap
- **1-cycle lookup**: on Send, HW resolves the `SlotIndex` in 1 clock cycle → dst, actor-id, perms
- **No cryptographic overhead**: no HMAC verify, no SipHash, no key management

### 2.c) Permission bits

| Bit | Flag | Meaning |
|---|---|---|
| 0 | Send | Send message to the target actor |
| 1 | Watch | Monitor target actor lifecycle |
| 2 | Stop | Stop the target actor |
| 3 | Delegate | Delegate CST slot (new slot, narrowed perms) |
| 4 | DelegateOnce | One-shot delegation (automatic revoke) |
| 5 | Migrate | Move the target actor to another core |
| 6–7 | MaxPri[2] | Maximum message priority (0–3), HW-enforced |

### 2.d) Delegation mechanism

CST delegation occurs on the **supervisor-to-supervisor VN0** channel:

```
Supervisor A (core 17)
    │  DelegateRequest { OrigSlot, TargetCore, RequestedPerms }
    │  → VN0 (management virtual network)
    ▼
Supervisor B (core 42)
    │  new_perms = RequestedPerms AND OriginalPerms   ← HW narrows
    │  CST.Allocate(new_entry) → new SlotIndex
    ▼
Response: TActorRef(newSlotIndex)   ← in target core's CST
```

**Invariant:** `new_perms ⊆ original_perms` — the HW AND operation ensures that delegation can **never escalate permissions** (seL4 capability monotonicity).

---

## Decision 3: Cross-chip addressing (inter-chip communication)

The 24-bit `dst` field is **a single chip's internal address space**. For multi-chip fabric, a proxy-actor pattern:

### 3.a) Rejected: flat global core-id

20+ bit flat core-id unnecessarily enlarges the CST table. The flat address space is a **non-existent abstraction** — the physical topology is hierarchical.

### 3.b) Rejected: hierarchical address inside the ref

`[chip-id:8][core-id:16][...]` decomposition only postpones the problem, and the user starts interpreting the ref (NOT opaque).

### 3.c) Decision: proxy-actor pattern

`vision-en.md` mandates location transparency:

> Actors can be migrated between cores at runtime — the same `Send(actor_ref, msg)` works in every case, and the router (hardware + software) decides where the message goes.

`TActorRef` is always **chip-local** (CST index into the local core's CST). Communication with a remote actor goes **through a proxy actor**:

```
Application actor (chip A, core 17)
        │
        │  Send(remoteProxyRef, msg)    ← local CST index
        ▼
[remote_proxy actor] (chip A, core 0)   ← in private state:
        │                                  { TargetChipId, TargetCoreId,
        │                                    TargetActorId, … }
        │  inter-chip link write (osreq-006)
        ▼
   Other chip → target actor
```

**Strengths:**
- 32-bit CST index is enough forever (chip-local meaning)
- The proxy actor's internal state carries the inter-chip routing information
- Location transparency: the application actor sees only refs
- Battle-tested: Akka.Remote, Erlang/OTP distributed, Pony ORCA

**Implementation milestones:**
- M0.6 Remoting: first proxy implementation with software TCP transport
- M0.7 CFPU HW Integration: proxy switches to `inter-chip link` MMIO (osreq-006)
- The proxy actor itself is supervised (M0.3) — restarts on crash

---

## Decision 4: Wire format — CLI-CPU interconnect header v3.0

CLI-CPU interconnect v3.0 header structure:

```
┌───────────┬───────┬─────────────────────────────────────────────────────────────┐
│   Field   │ Width │                          Meaning                            │
├───────────┼───────┼─────────────────────────────────────────────────────────────┤
│ dst       │ 24    │ Destination core HW address                                 │
│ dst_actor │ 8     │ Destination actor ID (max 256 actors / core)                │
│ src       │ 24    │ Source core HW address (HW-filled, unforgeable)             │
│ src_actor │ 8     │ Sender actor ID                                             │
│ seq       │ 16    │ Sequence number (fragmented messages)                       │
│ flags     │ 8     │ VN0/VN1, relay flag                                         │
│ len       │ 8     │ Payload byte count                                          │
│ reserved  │ 8     │ Reserved (zeroed)                                           │
│ CRC-16    │ 16    │ Header integrity (16 bit)                                   │
│ CRC-8     │ 8     │ Header CRC supplement                                       │
└───────────┴───────┴─────────────────────────────────────────────────────────────┘
                                                                       Σ = 128 bit
```

**Changes from v2.5 header:**
- **HMAC field removed** — no cryptographic verify needed, CST HW lookup replaces it
- **perms field removed from header** — permissions live in the CST, HW checks on Send
- **seq field expanded to 16 bits** (from 8) — larger fragmentation range
- **CRC-16 + CRC-8** — stronger header integrity protection (CRC-8 retained for compatibility)

**The `TActorRef` (CST index) is NOT bit-identical to the header.** On Send, the HW:
1. CST lookup: `SlotIndex` → `{dst, actor-id, perms}`
2. Perms check: if the Send flag is not set → trap
3. Header fill: `dst`, `dst_actor` from CST; `src`, `src_actor` filled by HW automatically

---

## Decision 5: CST defense model (replaces former HMAC algorithm)

### 5.a) Rejected: SipHash-128 HMAC verify (v2.0)

The previous model used SipHash-128 MSB-truncate to 24 bits for per-message HMAC verify.

**Why rejected:**
- ~5k gate / verify unit area cost **on every target core**
- ~10 cycle / verify latency on every message
- Key management complexity (rotation, per-core keys, counter thresholds)
- The CST model provides **the same protection without cryptographic cost**

### 5.b) Decision: CST HW lookup + SEAL protection

| Property | Value | Rationale |
|---|---|---|
| Defense mechanism | CST QSRAM lookup | HW-managed, software cannot modify |
| Lookup cycle | **1 cycle** @ 500 MHz = 2 ns | 10× faster than previous SipHash verify |
| HW area | ~1k gate (index decoder + comparator) | 5× smaller than SipHash verify unit |
| Invalid index | HW trap (supervisor IRQ + drop) | Fail-stop, as before |
| Perms enforce | CST entry perms AND operation | Same cycle, part of the lookup |

### 5.c) Defense pyramid (defense-in-depth) — v3.0

The CST model is **not cryptographic**, but a five-layer pyramid protects it:

```
┌────────────────────────────────────────────────────────────────────┐
│ 1. Compile-time:                                                   │
│    AuthCode SHA-256(bytecode) ↔ cert.PkHash binding                │
│    BitIce PQC signature                                            │
│    cert.SubjectId signer identity                                  │
├────────────────────────────────────────────────────────────────────┤
│ 2. Boot-time:                                                      │
│    Seal Core verify (BitIce trust chain)                           │
│    eFuse.CaRootHash (FenySoft master key SHA, OTP)                 │
│    revocation_list check (cert.SubjectId)                          │
├────────────────────────────────────────────────────────────────────┤
│ 3. Spawn-time:                                                     │
│    capability_registry (M2.5): signer-blacklist check              │
│    Bytecode SHA blacklist check                                    │
│    CST slot allocation: via supervisor HW trap                     │
├────────────────────────────────────────────────────────────────────┤
│ 4. Send-time:                                                      │
│    CST HW lookup: SlotIndex → {dst, actor-id, perms} (1 cycle)    │
│    Invalid index → HW trap + drop                                  │
│    perms verify: missing required flag → HW trap + drop            │
├────────────────────────────────────────────────────────────────────┤
│ 5. Quarantine trigger (on invalid CST access):                     │
│    HW fail-stop: sender core supervisor IRQ + drop                 │
│    AuthCode quarantine: cert.SubjectId → revocation_list           │
│    Bytecode SHA → blacklist                                        │
│    Running instances from that signer: supervisor terminate        │
│    Per-chip invalid-CST counter → threshold → alarm                │
└────────────────────────────────────────────────────────────────────┘
                                      │
                              Foundations:
                              Shared-nothing per-core SRAM isolation
                              Quench-RAM SEAL/RELEASE
                              CST QSRAM: SEAL-protected, HW-only write
```

**Why sufficient without cryptography:**
- Software **never sees** the actual dst address or permissions — only the CST index
- CST QSRAM is **SEAL-protected** — software writes are physically impossible
- Invalid CST index → immediate HW trap (not trial-and-error, but fail-stop)
- No brute-force attack vector: the CST index is either valid (result of a legitimate spawn) or invalid (immediate trap)

---

## Production deployment trust model

The defense level of the CST depends on the integrity of the **SEAL QSRAM**. The FenySoft product line ships **mandatorily in Strict whitelist mode**:

- `eFuse.CaRootHash` is a single slot, **OTP**, programmed with the FenySoft master key SHA
- Multi-root array, Open-mode bit, deployment-mode toggle: **NOT supported** (would be attack vectors)
- Customer bytecode signing only via FenySoft KYC + audit process
- CST slot allocation exclusively through supervisor kernel via HW trap

Detailed business model, justification of unsupported options, and multi-tier pricing: [`trust-model-en.md`](trust-model-en.md).

---

## What this means for code today (invariants)

Holding the 32-bit CST index contract pins down **two rules**:

1. **Forbidden** to write code assuming `SlotIndex = 0, 1, 2, …` is sequential. CST slot allocation is implementation-dependent — future refs may be "sparse" (deallocated slots, reuse).
2. **Forbidden** to leave the `int` type — no `string ActorName`, no `Guid`, no two-field record. The 32-bit CST index is the contract.

**Tests must not** rely on sequential indices either. Correct pattern: use the ref returned by `TActorSystem.Spawn`.

---

## Open questions

1. **CST size per core type** — Nano core (4 KB SRAM): how many CST slots fit? Actor core: how many? The QSRAM partitioning ratio between CST and actor data must be pinned down.
2. **CST slot garbage collection** — automatic release on actor death (supervisor-driven), or explicit revoke? Proposed: supervisor-driven automatic, explicit revoke optional.
3. **Shared use of proxy actors** — chip A's 100 actors all sending to chip B: shared `remote_proxy(chip B)`, or per-sender? Sharing is simpler but introduces back-pressure.
4. **Ref leakage across chip boundaries** — chip A actor passes a chip-A-internal ref to chip B actor: runtime detects and creates new proxy on chip B, or forbidden?
5. **CST overflow handling** — when a core's CST fills up, how should the supervisor react? Proposed: back-pressure on spawn (wait or trap to the requester).

---

## Related plans and milestones

- **`CLAUDE.md`** — 32-bit CST index public surface contract record
- **`docs/trust-model-en.md`** — FenySoft Strict whitelist business model, unsupported options
- ~~**`docs/osreq-to-cfpu/osreq-007-actor-ref-format-en.md`**~~ — **OBSOLETE** (HMAC-based header v2.5, mailbox-edge HMAC verify unit, counter, fail-stop — superseded by CST model)
- **`docs/roadmap.md` M0.6 — Remoting** — proxy-actor pattern first iteration
- **`docs/roadmap.md` M0.7 — CFPU Hardware Integration** — final CST implementation in silicon, MMIO mailbox integration
- **`docs/roadmap.md` M2.5 — Capability Registry** — kernel-side CST slot management and issuance, AuthCode integration
- **`docs/vision-en.md`** — capability-based security and location transparency design foundations
- **`CLI-CPU/docs/architecture-hu.md`** — 24-bit HW address, software-dispatched actor addressing
- **`CLI-CPU/docs/interconnect-hu.md`** — cell header structure (v3.0), tree topology, backpressure
- **`CLI-CPU/docs/ddr5-architecture-hu.md`** — CAM table for actor-level memory authorization
- **`CLI-CPU/docs/authcode-hu.md`** — AuthCode trust chain, SHA-256 binding, revocation
- **`CLI-CPU/docs/quench-ram-hu.md`** — SEAL/RELEASE invariant, cold-boot defense
- **`CLI-CPU/docs/security-hu.md`** — eliminated CWEs

---

## Version history

| Version | Date | Change |
|---|---|---|
| 1.0 | 2026-04-25 | Initial: 16-bit core-coord (undersized), 28-bit HMAC, proxy pattern |
| 2.0 | 2026-04-25 | Finalized specification: 64-bit ref, `[HMAC:24][perms:8][actor-id:8][core-coord:24]`, bit-identical with CLI-CPU 16-byte header, threat model, brute-force cost analysis, defense pyramid |
| 3.0 | 2026-04-28 | **MAJOR**: HMAC model eliminated, CST (Capability Slot Table) model introduced. `TActorRef(long ActorId)` 64-bit → `TActorRef(int SlotIndex)` 32-bit. SipHash/HMAC verify → 1-cycle CST HW lookup. Perms moved from header to CST. Header v3.0 (HMAC/perms fields removed, seq 16-bit, CRC-16). Brute-force analysis removed (not relevant). osreq-007 OBSOLETE. Delegation: supervisor-to-supervisor VN0, AND narrowing. seL4 capability analogy. |
