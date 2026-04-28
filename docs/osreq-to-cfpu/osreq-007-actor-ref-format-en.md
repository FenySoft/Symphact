# OSREQ-007: Actor Reference Format, HMAC Verify HW Unit, and Trust Anchor Hardening

> Magyar verzió: [osreq-007-actor-ref-format-hu.md](osreq-007-actor-ref-format-hu.md)

> Version: 1.0

> **Status:** Draft — awaiting hardware feedback
>
> **Affected CFPU phase:** F6 (silicon, ChipIgnite tape-out), F6.5 (Secure Edition optional hardening)

## Summary

Symphact has finalized the 64-bit `TActorRef` bit layout (`docs/actor-ref-scaling-en.md` v2.0). The specification places **three HW requirements** on the CLI-CPU team:

1. **Interconnect cell header v2.5**: `src_actor`/`dst_actor` reduced from 16 to 8 bits, with new `perms[8]` and `HMAC[24]` fields (the **header total stays 128 bits**)
2. **New HW component**: target-core mailbox-edge HMAC verify unit (per-core key, fail-stop trigger, bad-HMAC counter)
3. **Trust anchor hardening**: `eFuse` holds a single `CaRootHash` slot (NOT an array, NO Open-mode bit, NO deployment-mode toggle)

These are **not optional** — the Symphact defense pyramid relies on the HW-side implementation.

## Background — why now

`TActorRef` is the cornerstone of the Symphact public API. The v1.0 spec recorded in `roadmap.md` M0.7 — `[core-id:16][offset:16][HMAC:24][perms:8]` — was **incompatible** with CLI-CPU `interconnect-hu.md` v2.4 header and `architecture-hu.md` 24-bit HW address. The `actor-ref-scaling-en.md` v2.0 document resolves the inconsistencies and locks down a **bit-identical** layout with the canonical CLI-CPU widths:

```
TActorRef (64 bit, opaque, public):
[HMAC:24][perms:8][actor-id:8][core-coord:24]
   │        │         │              │
   ▼        ▼         ▼              ▼
Header HMAC  Header   Header dst_actor  Header dst
(new field)  perms    (new field)       (existing)
            (new field)
```

The CLI-CPU header is already 128 bits (16 bytes); the 64-bit TActorRef **maps onto its lower 64 bits** — the other 64 bits (src + src_actor + control) stay unchanged from v2.4, but **the allocation changes**.

## Requirement 1 — Interconnect cell header v2.5

### Current v2.4 (`interconnect-hu.md:72-99`)

```
Header (16 byte = 128 bit):
  dst[24] + src[24]                     — routing
  + src_actor[16] + dst_actor[16]       — actor ID
  + seq[8] + flags[8] + len[8] + CRC-8[8]  — control
  + reserved[16]                        — future
                                        Σ = 128 bit
```

### Proposed v2.5

```
Header (16 byte = 128 bit, SIZE UNCHANGED):
  dst[24] + src[24]                     — routing
  + src_actor[8] + dst_actor[8]         — actor ID (16→8 bit)
  + perms[8] + HMAC[24]                 — capability + auth (NEW)
  + seq[8] + flags[8] + len[8] + CRC-8[8]  — control
                                        Σ = 128 bit
```

| Field | v2.4 | v2.5 | Change |
|---|---|---|---|
| dst | 24 bit | 24 bit | — |
| src | 24 bit | 24 bit | — |
| src_actor | 16 bit | **8 bit** | reduction |
| dst_actor | 16 bit | **8 bit** | reduction |
| perms | (none) | **8 bit** | NEW |
| HMAC | (none) | **24 bit** | NEW |
| seq | 8 bit | 8 bit | — |
| flags | 8 bit | 8 bit | — |
| len | 8 bit | 8 bit | — |
| CRC-8 | 8 bit | 8 bit | — |
| reserved | 16 bit | (gone) | reused |

### Rationale for `actor-id` 16 → 8 bit

The v2.4 rationale ("max 65,536 actors / core, covers sleeping actors") imposes an **unnecessary ceiling**. Realistic actor count on a core SRAM:

| Core type | SRAM | Realistic actors/core |
|---|---|---|
| Nano (4 KB) | 4 KB | 1–10 |
| Actor (64 KB) | 64 KB | 10–100 |
| Rich (256 KB) | 256 KB | 50–500 |

8 bits (256 actors/core) is **more than sufficient**, and the freed 16 bits hold critical defense pyramid elements (perms + HMAC).

### Wire format ↔ TActorRef bit identity

The 64-bit `TActorRef` maps 1:1 onto the `dst[24] + dst_actor[8] + perms[8] + HMAC[24]` segment (no conversion in the runtime on Send). This is the most elegant wire-format-to-ref binding.

### DDR5 CAM table (`ddr5-architecture-hu.md:113-114`)

For consistency, the CAM table `src_actor[16]` field also reduces to **8 bits**:

```
| src[24]  | src_actor[8]  | DDR5 Start | DDR5 End | Right |
```

## Requirement 2 — Target-core mailbox-edge HMAC verify HW unit

### Goal

HMAC authentication of every incoming message at the target-core mailbox FIFO **input**, NOT in the interconnect router. This aligns with the CFPU `architecture-hu.md:1326` "one mailbox IRQ per core" principle.

### HW unit specification

```
Inputs:
  - Incoming cell header (128 bit from router)
  - Per-core HMAC key (256 bit, sealed in Quench-RAM / on-chip SRAM)

Computation:
  M       = dst[24] || dst_actor[8] || perms[8] || (constant nonce)
  K       = per_core_key
  tag_full = SipHash-128(K, M)            // ~10 cycle @ 500 MHz
  expected_HMAC = tag_full[127:104]        // MSB 24 bit (NIST SP 800-107)

Verification:
  if (header.HMAC == expected_HMAC):
      pass → cell into mailbox FIFO
  else:
      drop + fail-stop trigger + counter increment
```

### Cost estimate

| Item | Value |
|---|---|
| HW area / verify unit | ~5,000 gate (SipHash-128 ~3-4k + compare/control ~1-2k) |
| Verify cycle | ~10 cycle @ 500 MHz = **20 ns** |
| Per-chip cost (88k Nano cores) | ~440M gate ≈ **2.2 mm² at 5nm** (~0.15% of 1494 mm² package) |
| Throughput | 5 × 10⁷ verify/sec/core (sequential) |

### Algorithm choice — SipHash-128

| Algorithm | Area | Verify cycle | Fit |
|---|---|---|---|
| HMAC-SHA256 trunc | ~30k gate | ~80 cycle | General crypto, oversized for short-message MAC |
| HMAC-SHA3-256 trunc | ~50k gate | ~100 cycle | PQC-ready, too expensive |
| BLAKE3 | ~15k gate | ~30 cycle | Modern, fast, but not MAC-specific |
| **SipHash-128** | **~5k gate** | **~10 cycle** | Designed specifically for short-message MAC (Aumasson & Bernstein) |
| HMAC-MD5 trunc | ~10k gate | ~40 cycle | Cryptographically broken |

**SipHash-128** is the choice: 6× smaller and 8× faster than HMAC-SHA256, cryptographically strong for short-message MAC. NIST SP 800-107 allows MSB 24-bit truncation (1:16.8M forgery resistance), and combined with the Symphact defense pyramid (see `actor-ref-scaling-en.md` "Defense pyramid") provides post-quantum-grade security.

## Requirement 3 — HW fail-stop and bad-HMAC counter

### HW fail-stop

On a bad HMAC:

```
1. Cell drop (NOT placed in mailbox FIFO)
2. Supervisor IRQ trigger to the SENDER core (read from header.src field)
3. Sender core CORE_STATUS = Error (osreq-005 IRQ_MAILBOX_FAIL)
4. AuthCode quarantine trigger (see below)
```

This is the "1 bad HMAC = sender core terminated" pattern. The current `osreq-005` only sets `CORE_STATUS = Error`; it must be **extended** with the sender-side fail-stop trigger.

### Per-chip bad-HMAC counter

```
HW register: BAD_HMAC_COUNTER (32 bit, monotonic, NOT software-clearable)
Threshold:   16 (default, configurable in sealed register at BIST)

Increment behavior:
  bad_hmac_event → BAD_HMAC_COUNTER++
  if BAD_HMAC_COUNTER >= threshold:
      → IRQ to capability_registry
      → chip-wide key rotation
      → BAD_HMAC_COUNTER cleared (only via ROTATE)
      → supervisor alarm (audit log)
```

This is a **defense-in-depth layer**: even if the attacker bypasses the signer blacklist and HW fail-stop, after the 16th attempt a chip-wide key rotation invalidates **all prior brute-force progress**.

## Requirement 4 — AuthCode quarantine integration

The `authcode-hu.md:135` revocation_list must be extended with **automatic quarantine trigger on bad HMAC**:

```
Bad HMAC event:
1. Identify the sender bytecode SHA-256 from instances running on header.src core
2. Add this SHA to a new BAD_HMAC_BLACKLIST (new mechanism)
3. Add the signer key cert.SubjectId to revocation_list
4. Supervisor terminates ALL OTHER running bytecode from this signer
5. Add ALL prior bytecodes from this signer to blacklist (preventive)
```

**Rationale**: if any bytecode from a signer produces a bad HMAC, the signer is **proven compromised** or the attacker themselves. The "issuer-trust quarantine" pattern — one bad act = the signer is untrusted.

## Requirement 5 — Trust anchor hardening

### The `eFuse.CaRootHash` field

Per `authcode-hu.md:90`, the Seal Core verifies cert chains via `BitIce.Verify(cert, eFuse.CaRootHash)`. This is the **trust anchor** — every cert must trace back to this hash.

### Strict configuration

```
eFuse {
  CaRootHash : 256 bit                  ← OTP, SINGLE slot
                                        ← programmed with FenySoft master key SHA-256
                                        ← physically NOT modifiable post-manufacture
}

Forbidden:
  - Multi-root array (CaRootHash[0..N])
  - Open-mode bit / developer-chip bit
  - Deployment-mode toggle
  - Any runtime override mechanism for trust decisions
```

### Rationale

**Multi-root array** — post-manufacture attack vector: if the chip accepts multiple root hashes, an attacker can program a spare slot with their own key (even within warranty period). A **single OTP slot** is physically constrained: once programmed, never replaceable.

**Open-mode bit** — any "developer chip" distinguishing bit is an **attack surface**. The attacker sets it and now has a back-door. **The non-existent configuration** = the attacker cannot find a shortcut.

**Deployment-mode toggle** — would create a runtime trust decision, contradicting the `authcode-hu.md` SHA-256 binding mandatory invariant. Remove all such mechanisms.

The FenySoft product line **mandatorily** uses this strict configuration (see `docs/trust-model-en.md`). This is a foundational assumption of the Symphact defense pyramid.

## Impact estimate

| Requirement | HW effort | Tape-out risk |
|---|---|---|
| Header v2.5 field widths | RTL rearrangement of cell header SRAM (~1 week) | Low — size unchanged, only allocation differs |
| Mailbox-edge HMAC verify unit | New HW block (SipHash-128 + compare/control), per-core (~2 weeks) | Medium — new component, FPGA verification (F4 phase) |
| HW fail-stop extension | Extend `IRQ_MAILBOX_FAIL` (osreq-005) with sender-side IRQ (~3 days) | Low |
| Per-chip BAD_HMAC_COUNTER | New HW register + threshold compare + IRQ wiring (~3 days) | Low |
| Trust anchor hardening | OTP eFuse single-slot configuration (already a single slot in F6 design) | Low — already conforming, just no array-style extension |

**Estimated total effort**: ~3-4 engineer-weeks for the CFPU team for the F6-Silicon One pre-tape-out revision.

## Open questions

1. **HMAC counter threshold value** — proposed: 16. Requires CFPU team agreement.
2. **SipHash-128 vs alternatives** — should we evaluate BLAKE3 / Poly1305 / CMAC-AES variants? SipHash-128 is the default, but the CFPU team may review.
3. **Per-core key derivation** — KDF (HKDF) from a master key, or per-core random keys at manufacture? KDF is simpler (one sealed master), but `capability_registry` must hold the master.
4. **Inter-chip HMAC algorithm** (osreq-006) — same SipHash, or stronger (HMAC-SHA256 / Poly1305)? Off-chip communication has a tougher threat model.
5. **BAD_HMAC_BLACKLIST storage size** — kilobytes or megabytes? Determines the storage-saturation defense margin.

## Related plans and milestones

- **`Symphact/docs/actor-ref-scaling-en.md`** — TActorRef bit layout and defense pyramid
- **`Symphact/docs/trust-model-en.md`** — FenySoft Strict whitelist business model
- **`Symphact/docs/roadmap.md`** M0.6, M0.7, M2.5 — implementation milestones
- **`Symphact/docs/vision-en.md`** — capability-based security and location transparency
- **`CLI-CPU/docs/architecture-hu.md`** — 24-bit HW address, Actor Scheduling Pipeline
- **`CLI-CPU/docs/interconnect-hu.md`** — cell header v2.4 (to be updated to v2.5)
- **`CLI-CPU/docs/ddr5-architecture-hu.md`** — CAM table v2.4 (`src_actor` 16→8)
- **`CLI-CPU/docs/authcode-hu.md`** — AuthCode trust chain, revocation_list
- **`CLI-CPU/docs/quench-ram-hu.md`** — sealed key storage
- **`CLI-CPU/docs/security-hu.md`** — eliminated CWEs

## Changelog

| Version | Date | Summary |
|---------|------|---------|
| 1.0 | 2026-04-25 | Initial: header v2.5, mailbox-edge HMAC verify unit, fail-stop, counter, AuthCode quarantine integration, trust anchor hardening |
