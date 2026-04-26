# OS requirements for the CFPU hardware

> Magyar verzió: [README-hu.md](README-hu.md)

This directory collects **OS-driven requirements** surfaced during Symphact development that should be considered in the CFPU hardware design (tracked in [`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU)).

## Workflow

1. A Symphact developer discovers a hardware-shaped need (via profiling, hitting a software workaround, or encountering a design wall)
2. They open an issue in **this repo** using the [`osreq-for-cfpu`](../../.github/ISSUE_TEMPLATE/osreq-for-cfpu.md) template
3. If the requirement is substantial enough to warrant a durable artefact, a markdown document lands in **this directory**
4. A linked issue is opened in [`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU) with the `osreq-from-os` label, referencing the Symphact issue
5. Hardware designers (F2 RTL, F3 TT, F4 FPGA, F5 Rich core) consider the requirement in the relevant phase

## Why this matters

This is the **OS → HW feedback loop**. Without it, hardware and OS development drift apart, and every CPU/OS mismatch in history (x86 segmentation, Itanium VLIW, ARM big.LITTLE rollout, Spectre/Meltdown) becomes our story too.

The Apple M-series success is rooted in exactly this loop: macOS QoS classes informed P-core/E-core asymmetry, Core ML informed the Neural Engine, Keychain informed the Secure Enclave, and so on. We want open-source Symphact to do the same for the open-source CFPU.

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
