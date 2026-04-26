# Actor Reference Scaling and Defense Model — Symphact

> Magyar verzió: [actor-ref-scaling-hu.md](actor-ref-scaling-hu.md)

> Version: 2.0

> Status: finalized specification, basis for M0.6 / M0.7 / M2.5

This document records the **finalized bit layout**, **wire format**, and **defense-in-depth pyramid** of `TActorRef`. The reasoning walks through the design decisions: why 64 bits, why this allocation, why a 24-bit HMAC, and why this defense suffices for the Symphact target audience (consumer + enterprise + industrial critical).

> **Audience:** Symphact runtime developers, CFPU HW designers (sister repo: `FenySoft/CLI-CPU`), reviewers of the API contract, security auditors.

---

## Context

`TActorRef` is the **cornerstone** of the Symphact public API: every actor-to-actor communication flows through it. `CLAUDE.md` records the API contract:

```csharp
public readonly record struct TActorRef(long ActorId);   // 64 bit, opaque, public
```

The 64 bits are **chip-local** in meaning, and **bit-identical** to the lower segment of the CLI-CPU interconnect cell header (see the wire-format section below). The user does not reach into the runtime's internal representation — the `long` is an opaque token, relevant only as `Equals`/`GetHashCode`/Send-parameter.

Earlier spec inconsistencies (160-bit struct in vision-en.md, roadmap M0.7 `[16][16][24][8]`) created **incompatibilities** with the CLI-CPU side's 24-bit HW address and 16-bit `src_actor`/`dst_actor` fields. This v2.0 document resolves those inconsistencies.

---

## Threat model

The Symphact defense model focuses on **software + supply-chain** attacks. Physical-layer attacks are **out-of-scope** for this defense layer.

| Attack vector | Defense | Scope |
|---|---|---|
| Software brute-force (chip-internal or network) | HMAC verify + HW fail-stop + counter | **In-scope** |
| Another actor reads an actor's key | Shared-nothing per-core SRAM isolation | **In-scope** |
| Compromised signer (malicious software) | AuthCode + supply-chain quarantine | **In-scope** |
| Cold boot (post-power-off freezing) | Quench-RAM RELEASE atomic wipe (side effect) | **In-scope** |
| **Bus-MITM, chip-decap, FIB probing** | — | **Out-of-scope** (physical access — stealing the SSD is easier) |
| **Side-channel (timing, power)** | — | **Out-of-scope** at base (Secure Edition F6.5 covers it) |

This is the Linux/Windows/macOS commercial threat model, and only the Secure Element (F6.5) extends to physical defense. Production deployments must explicitly document this (see [`trust-model-en.md`](trust-model-en.md)).

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

**A 24-bit core-coord** (16M cores) is sufficient through F-9 generations — exactly matching the CLI-CPU `architecture-hu.md:1320` 24-bit HW address.

---

## Decision 1: `TActorRef` stays 64-bit as the API contract

### 1.a) Rejected: 160-bit struct (earlier `vision-en.md`)

A 4-field struct (CoreId int + MailboxIndex int + CapabilityTag long + Permissions int) pushes complexity to the user, and increases the HW interconnect header size.

**Why rejected:**
- A capability must be an **opaque token** — the developer should not know the contents
- 160-bit verbatim transit on the tree fabric adds ~5–10% area
- Complexity surfaces on the **usage side**, not the runtime side (where it belongs)

### 1.b) Rejected: 128-bit (`UInt128`) now

128 bits would resolve every bit-budget concern, **but**:
- The CLI-CPU interconnect cell header is **128-bit** = 16 bytes. Of those, 64 bits already cover the entire TActorRef field set (see Decision 2). A 128-bit ref would be redundant.
- Pre-alpha (0.1) version, API-break is cheapest now, **yet still unnecessary**

### 1.c) Decision: 64-bit, opaque, with chip-local meaning

`TActorRef` remains `readonly record struct(long ActorId)`. The 64 bits are chip-local — cross-chip addressing uses the proxy-actor pattern (see Decision 3).

---

## Decision 2: Allocation of the 64 bits

The CLI-CPU interconnect header and DDR5 CAM table already pin canonical field widths. `TActorRef` must be **bit-identical** to these so that Send requires no conversion.

### 2.a) Original roadmap `[core-id:16][offset:16][HMAC:24][perms:8]`

This is **incompatible** with the CLI-CPU side:
- `core-id:16` < CLI-CPU `dst[24]` (24-bit HW address)
- `offset:16` (mailbox offset) ≠ CLI-CPU `dst_actor[16]` (actor identifier)

The roadmap values were based on a **misreading** — the "mailbox offset" is actually the actor-id, and the HW address is 24 bits in the CFPU.

### 2.b) Y3 proposal: `[HMAC:64][reserved:16][perms:8][actor-id:16][core-coord:24]` (128 bit)

CLI-CPU-compatible at 128 bits, but **redundant**: the header is already 128 bits, and TActorRef fits in 64.

### 2.c) Decision: `[HMAC:24][perms:8][actor-id:8][core-coord:24]` (64 bit)

```
 63          40 39    32 31    24 23                            0
┌──────────────┬────────┬────────┬─────────────────────────────────┐
│  HMAC tag    │ perms  │actor-id│       core-coord                │
│   24 bit     │  8 bit │  8 bit │        24 bit                   │
└──────────────┴────────┴────────┴─────────────────────────────────┘
```

| Field | Width | Rationale |
|---|---|---|
| `core-coord` | 24 bit | = CLI-CPU `dst[24]` in interconnect header. 16M cores → sufficient through F-9+ generations |
| `actor-id` | 8 bit | 256 actors/core. CLI-CPU `dst_actor` reduced from 16 to 8 (see osreq-007) — typical core hosts 1–100 actors (SRAM-bound) |
| `perms` | 8 bit, **bit-flag** | 8 capability flags: Send, Stop, Watch, Delegate, Revoke, Query, Snapshot, Migrate |
| `HMAC` | 24 bit | SipHash-128 MSB-truncate. Defense is multi-layered (see Decision 5 and brute-force cost analysis) |

### 2.d) `actor-id` reduction rationale

CLI-CPU `interconnect-hu.md:75, 93` currently specifies `src_actor[16]` and `dst_actor[16]` ("max 65,536 actors / core"). This is an **unnecessary ceiling**:
- Nano core (4 KB SRAM): typically 1–10 actors
- Actor core (64 KB SRAM): typically 10–100 actors
- Rich core (256 KB SRAM): typically 50–500 actors

8 bits (256 actors/core) is more than sufficient; the freed 16 bits go to header `perms[8]` and `HMAC[24]` fields (see osreq-007 header v2.5 proposal).

---

## Decision 3: Cross-chip addressing (inter-chip communication)

The 24-bit core-coord is **a single chip's internal address space**. For multi-chip fabric, a proxy-actor pattern:

### 3.a) Rejected: flat global core-id

20+ bit flat core-id squeezes HMAC below the cryptographic floor. The flat address space is a **non-existent abstraction** — the physical topology is hierarchical.

### 3.b) Rejected: hierarchical address inside the ref

`[chip-id:8][core-id:16][...]` decomposition only postpones the problem, and the user starts interpreting the ref (NOT opaque).

### 3.c) Decision: proxy-actor pattern

`vision-en.md` mandates location transparency:

> Actors can be migrated between cores at runtime — the same `Send(actor_ref, msg)` works in every case, and the router (hardware + software) decides where the message goes.

`TActorRef` is always **chip-local**. Communication with a remote actor goes **through a proxy actor**:

```
Application actor (chip A, core 17)
        │
        │  Send(remoteProxyRef, msg)    ← local 64-bit ref
        ▼
[remote_proxy actor] (chip A, core 0)   ← in private state:
        │                                  { TargetChipId, TargetCoreId,
        │                                    InterChipHmac, … }
        │  inter-chip link write (osreq-006)
        ▼
   Other chip → target actor
```

**Strengths:**
- 64-bit ref is enough forever (chip-local meaning)
- Two HMACs, two key rings (chip-internal + inter-chip), defense-in-depth
- Location transparency: the application actor sees only refs
- Battle-tested: Akka.Remote, Erlang/OTP distributed, Pony ORCA

**Implementation milestones:**
- M0.6 Remoting: first proxy implementation with software TCP transport
- M0.7 CFPU HW Integration: proxy switches to `inter-chip link` MMIO (osreq-006)
- The proxy actor itself is supervised (M0.3) — restarts on crash

---

## Decision 4: Wire format — match with CLI-CPU interconnect header

CLI-CPU `interconnect-hu.md` v2.5 (osreq-007 proposed change) header structure:

```
┌───────────┬───────┬─────────────────────────────────────────────────────────────┐
│   Field   │ Width │                          Meaning                            │
├───────────┼───────┼─────────────────────────────────────────────────────────────┤
│ dst       │ 24    │ Destination core HW address                                 │
│ src       │ 24    │ Source core HW address (HW-filled, unforgeable)             │
│ src_actor │ 8     │ Sender actor ID — max 256 actors / core                     │
│ dst_actor │ 8     │ Destination actor ID                                        │
│ perms     │ 8     │ Actor capability bit-flags                                  │
│ HMAC      │ 24    │ SipHash-128 MSB-truncate                                    │
│ seq       │ 8     │ Sequence number (fragmented messages)                       │
│ flags     │ 8     │ VN0/VN1, relay flag                                         │
│ len       │ 8     │ Payload byte count                                          │
│ CRC-8     │ 8     │ Header integrity                                            │
└───────────┴───────┴─────────────────────────────────────────────────────────────┘
                                                                       Σ = 128 bit
```

The 64-bit `TActorRef` is **bit-identical** to the following header fields:
- `core-coord[24]` ↔ `dst[24]`
- `actor-id[8]` ↔ `dst_actor[8]`
- `perms[8]` ↔ `perms[8]`
- `HMAC[24]` ↔ `HMAC[24]`

**On Send, the runtime transfers the lower 64 bits 1:1 into the header**, no conversion.

---

## Decision 5: HMAC algorithm and defense pyramid

### 5.a) Rejected: HMAC-SHA256 truncate

~80 cycle HW verify, ~30k gate area. **Unnecessarily heavy** for short-message MAC.

### 5.b) Decision: SipHash-128 MSB-truncate to 24 bits

| Property | Value | Rationale |
|---|---|---|
| Algorithm | SipHash-128 | Specifically designed for short-message MAC (Aumasson & Bernstein) |
| Truncate | upper 24 bits | NIST SP 800-107 convention (MSB) |
| HW area | ~5k gate / verify unit | 6× smaller than HMAC-SHA256 |
| Verify cycle | ~10 cycle @ 500 MHz = 20 ns | 8× faster than HMAC-SHA256 |
| Forgery resistance | 1 : 16.8 million (alone) | Evaluated together with the defense pyramid |

### 5.c) Defense pyramid (defense-in-depth)

The 24-bit HMAC alone is **not sufficient** — a five-layer pyramid backs it:

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
├────────────────────────────────────────────────────────────────────┤
│ 4. Send-time:                                                      │
│    HMAC verify (target-core mailbox-edge HW unit, osreq-007)       │
│    perms verify (capability bit-flags)                             │
├────────────────────────────────────────────────────────────────────┤
│ 5. Quarantine trigger (on bad HMAC):                               │
│    HW fail-stop: sender core supervisor IRQ + drop                 │
│    AuthCode quarantine: cert.SubjectId → revocation_list           │
│    Bytecode SHA → blacklist                                        │
│    Running instances from that signer: supervisor terminate        │
│    Per-chip bad-HMAC counter increment → threshold crossed:        │
│      chip-wide key rotation (capability_registry) + alarm          │
└────────────────────────────────────────────────────────────────────┘
                                      │
                              7. Foundations:
                              Shared-nothing per-core SRAM isolation
                              Quench-RAM SEAL/RELEASE
```

---

## Brute-force cost analysis with realistic throughput

Earlier pessimistic estimates (88k core × 1 GHz × 1 verify/cycle = 10¹³ tries/sec) are **absurd upper bounds**. Realistic numbers:

### Throughput limits

```
SipHash verify HW unit:        10 cycle / verify @ 500 MHz = 20 ns
Per target-core mailbox-edge:  sequential, ~5 × 10⁷ verify/sec MAX (verify bottleneck)
Cross-region roundtrip:        2 × 139 cycle + verify ≈ 600 ns @ 500 MHz
Hot-spot backpressure:         88k cores → 1 target = mailbox FIFO 8–64 deep saturates,
                               87,936 cores blocked
```

The attacker **must wait for a response** to know which HMAC was successful (in the Symphact shared-nothing model there is no inter-actor side-channel). Throughput: **~10⁶ tries/sec** aggregate.

### Bytecode-level gating (Open mode)

Even if the attacker tries to bypass the signer blacklist with new certs, **every new attempt = new bytecode + new cert + new load**:

| Step | Time |
|---|---|
| Random HMAC tag in bytecode payload | < 1 ns |
| Bytecode rebuild (new SHA-256) | ~1 ms |
| PQC self-signing (new cert) | ~10–100 ms |
| Bytecode + cert load to chip | **~1–10 sec** ← bottleneck |
| AuthCode Seal Core verify | ~1 ms |
| Spawn actor + Send + roundtrip | ~1 ms |

**Per-attempt total time (Open mode): ~1–10 seconds**.

### Brute-force time table

| HMAC bit | Keyspace | Strict mode (FenySoft KYC, ~3 days/signer) | Open mode (~1 sec/try) |
|---|---|---|---|
| **24** (chosen) | 1.7 × 10⁷ | **~70,000 years** | ~6 months–5 years |
| 32 | 4.3 × 10⁹ | ~36 million years | ~140 years |
| 48 | 2.8 × 10¹⁴ | ~2 trillion years | ~9 million years |
| 64 | 9.2 × 10¹⁸ | ~10¹⁷ years | ~290 billion years |

Plus **storage saturation** defense: 16.8M attempts × 32-byte SHA = 537 MB blacklist storage, far exceeding the chip's on-chip blacklist capacity (kilobyte–megabyte) → the attack stalls before success, with `MEM_FULL` admin alarm.

### Conclusion

The **24-bit HMAC + defense pyramid** combination:
- **Strict mode**: ~70,000 years brute-force defense (FenySoft Strict whitelist)
- **Open mode**: ~6 months–5 years brute-force defense (only relevant on developer chips, **NOT EXISTENT in the FenySoft product** — see `trust-model-en.md`)

Both levels are **post-quantum-grade for the Symphact target audience** (consumer, IoT, enterprise, industrial critical). For nation-state-level attacks the Secure Edition (F6.5) provides separate defense (`CLI-CPU/docs/secure-element-en.md`).

---

## Production deployment trust model

The defense level of the 24-bit HMAC depends on the integrity of the **gating function**. The FenySoft product line ships **mandatorily in Strict whitelist mode** with a FenySoft-controlled signer pool:

- `eFuse.CaRootHash` is a single slot, **OTP**, programmed with the FenySoft master key SHA
- Multi-root array, Open-mode bit, deployment-mode toggle: **NOT supported** (would be attack vectors)
- Customer bytecode signing only via FenySoft KYC + audit process

Detailed business model, justification of the unsupported options, and multi-tier pricing: [`trust-model-en.md`](trust-model-en.md).

---

## What this means for code today (invariants)

Holding the 64-bit contract pins down **two rules**:

1. **Forbidden** to write code assuming `ActorId = 1, 2, 3, …` is sequential. E.g., `for (long i = 0; i < count; i++)` ref iteration is an **anti-pattern** — future refs are "sparse" (HMAC random, core-coord sparse).
2. **Forbidden** to leave the `long` type — no `string ActorName`, no `Guid`, no two-field record. The 64 bits are the contract.

**Tests must not** rely on sequential IDs either. Correct pattern: use the ref returned by `TActorSystem.Spawn`.

---

## Open questions

1. **HMAC counter threshold value** — proposed: 16 (false-positive minimum, brute-force max bound). Part of osreq-007, requires CFPU team agreement.
2. **Per-core HMAC key rotation frequency** — automatic rotation on counter threshold (event-driven), or scheduled (timed)? Proposed: event-driven, since timed rotation adds complexity without value.
3. **Inter-chip HMAC algorithm** — same SipHash-128, or stronger (HMAC-SHA256)? The off-chip threat profile is harsher; stronger may be warranted. Part of osreq-006.
4. **Shared use of proxy actors** — chip A's 100 actors all sending to chip B: shared `remote_proxy(chip B)`, or per-sender? Sharing is simpler but introduces back-pressure.
5. **Ref leakage across chip boundaries** — chip A actor passes a chip-A-internal ref to chip B actor: runtime detects and creates new proxy on chip B, or forbidden?

---

## Related plans and milestones

- **`CLAUDE.md`** — 64-bit public surface contract record
- **`docs/trust-model-en.md`** — FenySoft Strict whitelist business model, unsupported options
- **`docs/osreq-to-cfpu/osreq-007-actor-ref-format-en.md`** — HW requirements (header v2.5, mailbox-edge HMAC verify unit, counter, fail-stop, single-root eFuse)
- **`docs/roadmap.md` M0.6 — Remoting** — proxy-actor pattern first iteration
- **`docs/roadmap.md` M0.7 — CFPU Hardware Integration** — final bit-layout in silicon, MMIO mailbox integration
- **`docs/roadmap.md` M2.5 — Capability Registry** — kernel-side HMAC key management and issuance, AuthCode integration
- **`docs/vision-en.md`** — capability-based security and location transparency design foundations
- **`CLI-CPU/docs/architecture-hu.md`** — 24-bit HW address, software-dispatched actor addressing
- **`CLI-CPU/docs/interconnect-hu.md`** — cell header structure, tree topology, backpressure
- **`CLI-CPU/docs/ddr5-architecture-hu.md`** — CAM table for actor-level memory authorization
- **`CLI-CPU/docs/authcode-hu.md`** — AuthCode trust chain, SHA-256 binding, revocation
- **`CLI-CPU/docs/quench-ram-hu.md`** — SEAL/RELEASE invariant, cold-boot defense
- **`CLI-CPU/docs/security-hu.md`** — eliminated CWEs

---

## Version history

| Version | Date | Change |
|---|---|---|
| 1.0 | 2026-04-25 | Initial: 16-bit core-coord (undersized), 28-bit HMAC, proxy pattern |
| 2.0 | 2026-04-25 | **Finalized specification**: 64-bit ref, `[HMAC:24][perms:8][actor-id:8][core-coord:24]`, bit-identical with CLI-CPU 16-byte header, threat model section, brute-force cost analysis with realistic throughput, defense pyramid, FenySoft Strict whitelist reference, justification of unsupported options |
