# Trust Model and FenySoft Strict Whitelist Business Model

> Magyar verzió: [trust-model-hu.md](trust-model-hu.md)

> Version: 1.1

> Status: finalized deployment policy, basis for F6-Silicon One and later tape-outs

This document records the **trust model** of the Symphact / CFPU product line: who issues bytecode-signing identities, how customers obtain signing identities, and **which options are explicitly NOT supported** for security reasons.

> **Audience:** FenySoft product management, customer partners (chip integrators, OEMs), security audits, legal review.

---

## Trust model in brief

CFPU chips manufactured by FenySoft ship **mandatorily in Strict whitelist mode**. A single trust anchor (FenySoft master key), a single signer pool (FenySoft-controlled), a single revocation list. No runtime configuration override, no developer-mode toggle, no multi-root option.

```
Manufacture (FenySoft):
   eFuse.CaRootHash := SHA-256(FenySoft master public key)
                       ↑
                       OTP, SINGLE slot, physically immutable post-manufacture

Operation:
   Every bytecode signed with FenySoft cert (or delegated subordinate CA cert).
   Invalid CST slot = signer immediately blacklisted (issuer-trust quarantine).
```

This model establishes the integrity of the CST (Capability Slot Table) HW-managed capability model.

---

## Trust chain from manufacture to runtime verification

```
1. CFPU manufacture (FenySoft):
   ┌──────────────────────────────────────────────────────────────┐
   │  Tape-out / packaging:                                       │
   │    eFuse.CaRootHash := SHA-256(FenySoft_root_public_key)     │
   │  ⇒ Fixed for the LIFETIME of the chip.                       │
   │  ⇒ FenySoft holds the private root key in master HSM.        │
   └──────────────────────────────────────────────────────────────┘

2. Customer requests bytecode signing:
   ┌──────────────────────────────────────────────────────────────┐
   │  Customer → FenySoft:                                        │
   │    - bytecode SHA-256                                        │
   │    - customer ID (company, project)                          │
   │    - KYC documents                                           │
   │                                                              │
   │  FenySoft (KYC + security audit):                            │
   │    - Verify customer identity                                │
   │    - Bytecode review (static analysis, opcode allowlist)     │
   │    - Signing:                                                │
   │      cert = sign(FenySoft_HSM, {                             │
   │        PkHash    = SHA-256(bytecode),                        │
   │        SubjectId = customer-id                               │
   │      })                                                      │
   │                                                              │
   │  FenySoft → Customer: cert (alongside the CIL binary)        │
   └──────────────────────────────────────────────────────────────┘

3. Customer loads onto chip (bytecode + cert).

4. Seal Core verify (boot-time):
   ┌──────────────────────────────────────────────────────────────┐
   │  1. SHA-256(bytecode) == cert.PkHash ?           ← binding   │
   │  2. BitIce.Verify(cert, eFuse.CaRootHash) ?      ← trust ←┐  │
   │  3. cert.SubjectId ∉ revocation_list ?           ← quar.  │  │
   │                                                           │  │
   │  All OK → CODE region SEAL → Spawn allowed                │  │
   └───────────────────────────────────────────────────────────┘  │
                                                                  │
   Trust anchor: FenySoft root key — only FenySoft can delegate ──┘
```

---

## Business model tiers

FenySoft can offer customers a **multi-tier** pricing structure:

| Tier | What the customer gets | Pricing basis |
|---|---|---|
| **Per-cert** | Single cert for a specific bytecode | $100-1000 / cert |
| **Subscription** | Annual developer subscription, unlimited certs for an app family | $500-5000 / year |
| **Enterprise CA delegation** | Subordinate CA — customer gets own CA under FenySoft chain | $50k-500k / year (with compliance audit) |
| **Compliance tier** | Audited + supported license, with legal liability (ISO 26262, IEC 61508, FIPS 140-3) | $500k+ / year |

The right tier depends on customer volume, compliance needs, and business model:

- **IoT / consumer device manufacturer**: per-cert or subscription
- **Enterprise app developer**: subscription
- **Automotive Tier 1, industrial manufacturer**: enterprise CA delegation
- **Financial institution, telecom, energy**: compliance tier

---

## NOT-supported options — security rationale

The following options FenySoft **explicitly rejects** in the CFPU product line, because each would create an **attack vector**.

### NOT: Multi-root eFuse array

**What it would mean:** multiple `CaRootHash` slots in the eFuse (FenySoft + customer's own + spares), allowing the customer to maintain their own signer pool.

**Why NOT:**
- Spare slots are an **attack surface**: a post-manufacture attacker (even within warranty period) could program their own root key into a spare slot, then run any bytecode on the chip
- The "customer root" business benefit is **NOT worth more** than the hardware single-point-of-trust security
- The Enterprise CA delegation tier (subordinate CA) provides **the same** service **without** needing more chip-level slots

**Replacement:** Enterprise CA delegation tier — the customer gets a subordinate CA that operates under the FenySoft chain, providing full control over their signer pool.

### NOT: Open-mode eFuse bit (developer-chip vs production-chip)

**What it would mean:** an eFuse bit that enables "developer mode" where the Seal Core would accept self-signed bytecode (without FenySoft cert).

**Why NOT:**
- Any runtime-configurable trust decision is an **attack vector**: the attacker sets the bit, defense suspended
- "Developer-chip" distinction is a **runtime trust decision** that contradicts the `authcode-hu.md` SHA-256 binding mandatory invariant
- The right model for developers: dedicated developer subscription tier (low-priced, fast KYC), NOT a separate chip configuration

**Replacement:** Developer subscription tier — low-priced, fast-turnaround FenySoft cert issuance for development use cases.

### NOT: Deployment-mode toggle / runtime override

**What it would mean:** some runtime mechanism to suspend the trust model (e.g., a special signal at boot, or a "trusted admin" message).

**Why NOT:**
- Any runtime switch on the trust model becomes **the attacker's target**
- The defense foundation: **no configuration exists** to bypass or toggle
- The AuthCode `SHA-256(bytecode) ↔ cert.PkHash` binding is a **mandatory invariant** — no runtime mechanism may break it

**Replacement:** None. The trust model is **statically locked** for the lifetime of the chip.

### NOT: "Open mode" deployment in the FenySoft product line

**What it would mean:** the chip ships in "Open mode" where anyone can run any bytecode (with or without FenySoft cert).

**Why NOT:**
- The FenySoft product brand is a **direct consequence** of Strict whitelist mode — that's what gives reliability
- Open mode chips with "FenySoft Verified" mark = brand integrity damage
- For those who need Open mode (e.g., entirely independent ecosystem development), the CLI-CPU open-source allows them to **manufacture their own chip** with their own root key — that's the CERN-OHL-S license option

**Replacement:** The customer can manufacture their own chip from CLI-CPU open-source RTL, with their own trust anchor. **This is NOT "FenySoft Verified Symphact chip"** — different product, different brand, different category.

---

## Open-source vs closed trust chain — resolving the tension

Symphact is Apache-2.0, CLI-CPU is CERN-OHL-S — **open licenses**. The closed trust chain may seem contradictory, but **does not violate the licenses**:

| Layer | License / status | Who controls |
|---|---|---|
| Symphact source code | Apache-2.0 | Anyone, forkable, modifiable |
| CLI-CPU RTL, ISA spec, simulator | CERN-OHL-S | Anyone, forkable, manufacturable |
| **A specific chip's eFuse content** | **Manufacturer's decision** | The chip's manufacturer (e.g., FenySoft) |
| **"FenySoft Verified" product brand** | **Trademark** | FenySoft exclusively |

This is the **Android model**:
- AOSP (Android Open Source Project) is Apache-2.0 — anyone can use it
- Google Play Services + Google Apps are closed — only on Google-signed devices
- Manufacturers must pass Google CTS (Compatibility Test Suite)

Likewise for Symphact:
- **Plain CFPU** (own manufacture, own root): the customer can run their own bytecode with their own signing. **Not "FenySoft Verified"** — different category.
- **FenySoft Verified CFPU**: the official FenySoft product, with FenySoft root, FenySoft cert issuance, FenySoft support.

The customer **chooses**:
- **Cheaper, faster, freer**: plain CFPU in own manufacture, own trust chain
- **More expensive, more reliable, FenySoft-supported**: FenySoft Verified product

---

## What the trust model gives back for the developer friction

The FenySoft Strict whitelist model creates **developer friction**:

| Friction | What it enables |
|---|---|
| New cert issuance takes days–weeks (KYC, audit) | Brand integrity, reputation, security |
| Customer must request signing from FenySoft | Recurring revenue, central revocation, audit trail |
| Apache-2.0 / CERN-OHL-S openness seemingly limited | Hardware-level security guarantees |

In return, the customer gets:

| Guarantee | Effect |
|---|---|
| Hardware-level CST defense | HW-managed capability table, with FenySoft-controlled gating |
| Central revocation | A malware bytecode can be revoked globally by FenySoft — across all chips, instantly |
| Compliance trail | Every cert issuance auditable (GDPR, ISO 26262, IEC 61508, FIPS 140-3) |
| Brand trust mark | "FenySoft Verified" quality mark on the product |
| Security audit | FenySoft cert issuance includes bytecode review |

For the **majority** of consumer / IoT / enterprise / industrial-critical target audience, the FenySoft model provides **dramatic value**: no need to build own security infrastructure, the HW-level defense is ready and working.

---

## eFuse OTP configuration locked

(**Note:** osreq-007 is OBSOLETE — superseded by the CST model.) The trust anchor hardening:

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

This requirement targets the CFPU team (`FenySoft/CLI-CPU` repo), and applies to F6-Silicon One and later tape-outs.

---

## F6.5 Secure Edition relationship to the trust model

Per `CLI-CPU/docs/secure-element-hu.md`, the **Secure Edition (F6.5)** is an optional chip variant that **complements** (does NOT replace) the Strict whitelist model:

- F6 (base): Strict whitelist + physical attack out-of-scope
- **F6.5 Secure**: Strict whitelist + physical tamper resistance + side-channel countermeasures + PUF + TRNG

The Secure Edition provides defense against **nation-state-level attacks** (dedicated anti-FIB, anti-decap, mesh, side-channel countermeasures). The trust model is **identical in both variants** — only the physical-layer defense differs.

---

## Related documents

- **`actor-ref-scaling-en.md`** — TActorRef bit layout and defense pyramid
- ~~**`osreq-to-cfpu/osreq-007-actor-ref-format-en.md`**~~ — **OBSOLETE** (superseded by CST model)
- **`vision-en.md`** — capability-based security design foundations
- **`roadmap.md`** M2.5 — Capability Registry implementation
- **`CLI-CPU/docs/authcode-hu.md`** — AuthCode trust chain specification
- **`CLI-CPU/docs/secure-element-hu.md`** — F6.5 Secure Edition (optional hardening)
- **`CLI-CPU/docs/security-hu.md`** — eliminated CWEs

---

## Changelog

| Version | Date | Summary |
|---------|------|---------|
| 1.1 | 2026-04-28 | **HMAC verify replaced by CST HW lookup**. SipHash references removed. osreq-007 reference marked OBSOLETE. Trust model mechanism updated to CST-based; the essence (Strict whitelist, single trust anchor) is unchanged. |
| 1.0 | 2026-04-25 | Initial: FenySoft Strict whitelist business model, multi-tier pricing, explicit list of NOT-supported options with security rationale, Android-model analogy, F6.5 Secure Edition relationship |
