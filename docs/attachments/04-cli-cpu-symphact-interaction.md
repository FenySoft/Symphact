# Attachment 4 — CLI-CPU ↔ Symphact Interaction Diagram

> NLnet NGI Zero Commons Fund — Symphact application, 13th open call (deadline 2026-06-01).
> Co-design feedback loop between the Symphact actor runtime and the CLI-CPU / CFPU open silicon.

## 1. The two repositories, deliberately separated

| Dimension | `FenySoft/Symphact` | `FenySoft/CLI-CPU` |
|---|---|---|
| **Deliverable** | Software runtime, OS services | Hardware ISA, RTL, silicon tape-out, FPGA |
| **Target** | .NET 10 library (Windows / Linux / macOS) | Verilog synthesis, Sky130 PDK |
| **License** | Apache-2.0 (permissive) | CERN-OHL-S-2.0 (reciprocal hardware) |
| **Funded milestones** | Actor runtime + kernel actors | F2 RTL, F3 Tiny Tapeout, F4 FPGA multi-core |
| **Dependency** | None on CLI-CPU silicon (simulator suffices) | None on Symphact |

Both projects are developed by the same applicant — **two open projects, one developer, one openly documented co-design loop**.

## 2. The OS → HW feedback workflow

```
┌─────────────────────────────────────────────────────────────────┐
│  Symphact developer working on an OS milestone                  │
│  (e.g. M0.5 content-addressed journal, M2.5 capability reg, ...)│
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼  hits a hardware-shaped need
                         │  (profiling, software workaround,
                         │   design wall)
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│  1. Issue opened in FenySoft/Symphact using template            │
│     `.github/ISSUE_TEMPLATE/osreq-for-cfpu.md`                  │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│  2. Durable markdown lands in `docs/osreq-to-cfpu/`             │
│     bilingual (en + hu), versioned, status: Draft               │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│  3. Linked issue opened in FenySoft/CLI-CPU                     │
│     label: `osreq-from-os`, references Symphact issue           │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│  4. CLI-CPU designers consider in the relevant phase            │
│     (F2 RTL, F3 Tiny Tapeout, F4 FPGA, F5 Rich core, F6 silicon)│
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
                  Decision = HW implements
                       OR explicit "won't fix"
                       — both are documented under
                       Apache-2.0 / CERN-OHL-S
```

## 3. Active osreq backlog at submission

Six active OS-requirements landed during the M0.1 → M0.5 work. Each is bilingual, versioned, and linked back to the originating Symphact milestone.

| # | Title | CFPU phases | Status | Origin (Symphact milestone) |
|---|---|---|---|---|
| OSREQ-001 | Tree-structured interconnect between cores | F4, F5, F6 | Draft | M0.4 scheduler |
| OSREQ-002 | MMIO memory map — OS↔HW register interface | F4, F5, F6 | Draft | M0.7 (planned), device actor framework |
| OSREQ-003 | Core reset mechanism — supervisor restart | F4, F5, F6 | Draft | M0.3 supervision |
| OSREQ-004 | DMA engine — non-blocking persistence | F5, F6 | Draft | M0.5 persistence |
| OSREQ-005 | Mailbox interrupt vs polling — core notification | F4, F5, F6 | Draft | M0.4 scheduler |
| OSREQ-006 | Inter-chip link protocol — distributed fabric | F6, F7 | Draft | M0.6 remoting (planned) |

OSREQ-007 (actor-ref format) was **superseded by the CST model** during the M0.3/M0.4 work — documented as obsolete, with the new CST model integrated into both repos (`docs/trust-model-en.md` v1.1).

## 4. Expected feedback during the grant (3–7 new osreqs)

Each grant milestone is paired with a **measurement sub-slice** designed to surface concrete HW requirements. Expected feedback paths over the 12 months:

| Grant milestone | Expected `osreq-to-cfpu` issue (1–2 each) |
|---|---|
| **M1: Persistence** | SHA-256 / BLAKE3 hash instruction (precedent: ARM SHA extensions, Intel SHA-NI); HW mailbox FIFO depth tuning |
| **M2: Remoting + Capability Registry** | CST cache or HW lookup-assistance; capability invalidation broadcast primitive |
| **M3: Kernel + Device actors** | Supervisor fault-notification line (vs. mailbox watchdog); MMIO mapping discipline |
| **M4: CFPU integration demo** | SRAM-to-SRAM DMA for actor-state migration, or an explicit "not needed at this scale" conclusion |
| **M6: Formal-verification foundations** | Optional global scheduling clock or sync primitive for verification runs |

Total expected: **3–7 concrete `osreq-to-cfpu` issues** across the 12 months — each backed by a benchmark measurement from the simulator (per the discipline rule: *no requirement without data*). Negative results count: a "won't fix in silicon" conclusion is also useful HW guidance.

## 5. Historical precedents for OS↔HW co-design

The osreq-to-cfpu directory README cites three documented precedents:

| Precedent | Year | What was co-designed |
|---|---|---|
| **Inmos Transputer + Occam** | ~1984 | HW mailbox channels and the Occam `chan` primitive — message passing as an ISA + language feature simultaneously |
| **Symbolics Lisp Machine** | 1985 (Moon: "Architecture of the Symbolics 3600") | Tagged pointer ISA and GC hardware support, co-designed with the Lisp runtime |
| **Google TPU + TensorFlow** | 2017 (Jouppi et al., ISCA) | Published co-design where workload requirements measurably shaped chip microarchitecture |

Apple's M-series chips are vertically integrated, but the **concrete co-design process between HW and OS is not publicly documented**. Open-source Symphact + CFPU aims to **break that opacity**: every feedback decision is public under Apache-2.0 / CERN-OHL-S, reproducible, and tracked as an `osreq-to-cfpu` issue.

## 6. Why this matters for NGI

NGI mission alignment:

- **European paradigm sovereignty** — open licenses, no US/Asian IP dependencies
- **Reproducible engineering** — every feedback decision is documented and verifiable
- **Avoids historical OS/HW mismatches** — x86 segmentation, Itanium VLIW, ARM big.LITTLE rollout, Spectre/Meltdown all originated in HW/OS drift
- **Bidirectional, not waterfall** — the OS does not just "use" the HW; the HW design is shaped by measured OS needs

The same applicant developing both projects means osreq turnaround is **measured in days, not months**, and the next CFPU RTL iteration directly absorbs the OS findings — no separate vendor negotiation, no NDA wall.
