# OS requirements for the CFPU hardware

> Magyar verzió: [README-hu.md](README-hu.md)

> Version: 1.0

This directory collects **OS-driven requirements** surfaced during Symphact development that should be considered in the CFPU hardware design (tracked in [`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU)).

## Workflow

1. A Symphact developer discovers a hardware-shaped need (via profiling, hitting a software workaround, or encountering a design wall)
2. They open an issue in **this repo** using the [`osreq-for-cfpu`](../../.github/ISSUE_TEMPLATE/osreq-for-cfpu.md) template
3. If the requirement is substantial enough to warrant a durable artefact, a markdown document lands in **this directory**
4. A linked issue is opened in [`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU) with the `osreq-from-os` label, referencing the Symphact issue
5. Hardware designers (F2 RTL, F3 TT, F4 FPGA, F5 Rich core) consider the requirement in the relevant phase

## Why this matters

This is the **OS → HW feedback loop**. Without it, hardware and OS development drift apart, and every CPU/OS mismatch in history (x86 segmentation, Itanium VLIW, ARM big.LITTLE rollout, Spectre/Meltdown) becomes our story too.

Documented historical precedents include the **Inmos Transputer** (Inmos technical papers, ~1984) — HW mailbox channels and the Occam `chan` primitive co-designed together; the **Symbolics Lisp Machine** (Moon, "Architecture of the Symbolics 3600", 1985) — tagged pointer ISA and GC hardware support for the Lisp runtime; and the **Google TPU + TensorFlow** (Jouppi et al., ISCA 2017) — published co-design where workload requirements measurably shaped chip microarchitecture. Apple's M-series is vertically integrated, but the **concrete co-design process between HW and OS is not publicly documented**. Open-source Symphact + CFPU aims to **break that opacity**: every feedback decision is public under Apache-2.0 / CERN-OHL-S, reproducible, and tracked as an `osreq-to-cfpu` issue.

## Current open requirements

*(populated as Symphact development progresses)*

| # | Title | CFPU phase | Status |
|---|-------|------------|--------|
| [OSREQ-001](osreq-001-tree-interconnect-en.md) | Tree-structured interconnect between cores | F4, F5, F6 | Draft |
| [OSREQ-002](osreq-002-mmio-memory-map-en.md) | MMIO memory map — OS↔HW register interface | F4, F5, F6 | Draft |
| [OSREQ-003](osreq-003-core-reset-en.md) | Core reset mechanism — supervisor restart | F4, F5, F6 | Draft |
| [OSREQ-004](osreq-004-dma-engine-en.md) | DMA engine — non-blocking persistence | F5, F6 | Draft |
| [OSREQ-005](osreq-005-mailbox-interrupt-en.md) | Mailbox interrupt vs polling — core notification | F4, F5, F6 | Draft |
| [OSREQ-006](osreq-006-interchip-link-en.md) | Inter-chip link protocol — distributed fabric | F6, F7 | Draft |

---

## Changelog

| Version | Date | Summary |
|---------|------|---------|
| 1.0 | 2026-04-16 | Initial release |
