# OS requirements for the CFPU hardware

> Magyar verzió: [README-hu.md](README-hu.md)

This directory collects **OS-driven requirements** surfaced during Neuron OS development that should be considered in the CFPU hardware design (tracked in [`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU)).

## Workflow

1. A Neuron OS developer discovers a hardware-shaped need (via profiling, hitting a software workaround, or encountering a design wall)
2. They open an issue in **this repo** using the [`osreq-for-cfpu`](../../.github/ISSUE_TEMPLATE/osreq-for-cfpu.md) template
3. If the requirement is substantial enough to warrant a durable artefact, a markdown document lands in **this directory**
4. A linked issue is opened in [`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU) with the `osreq-from-os` label, referencing the Neuron OS issue
5. Hardware designers (F2 RTL, F3 TT, F4 FPGA, F5 Rich core) consider the requirement in the relevant phase

## Why this matters

This is the **OS → HW feedback loop**. Without it, hardware and OS development drift apart, and every CPU/OS mismatch in history (x86 segmentation, Itanium VLIW, ARM big.LITTLE rollout, Spectre/Meltdown) becomes our story too.

The Apple M-series success is rooted in exactly this loop: macOS QoS classes informed P-core/E-core asymmetry, Core ML informed the Neural Engine, Keychain informed the Secure Enclave, and so on. We want open-source Neuron OS to do the same for the open-source CFPU.

## Current open requirements

*(populated as Neuron OS development progresses)*

| # | Title | CFPU phase | Status |
|---|-------|------------|--------|
| — | (none yet — development just started) | — | — |
