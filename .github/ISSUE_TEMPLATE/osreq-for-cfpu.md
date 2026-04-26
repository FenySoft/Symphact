---
name: "OS requirement for CFPU hardware"
about: "An OS-level need that should be considered in the CFPU hardware design (CLI-CPU)"
title: "[osreq] "
labels: ["osreq-to-cfpu", "hw-codesign"]
assignees: []
---

## Summary

<!-- One sentence: what does Symphact need from the CFPU hardware? -->

## Context

<!-- Where in Symphact did this requirement surface?
  - Which module/actor/test revealed it?
  - Was it a performance bottleneck, a correctness issue, or a design limitation? -->

## Current software workaround

<!-- How is Symphact handling this today without hardware support?
     Is the workaround acceptable for v0.x, or is it blocking something? -->

## Proposed hardware behaviour

<!-- What should the CFPU provide?
  - New MMIO register?
  - Different mailbox depth?
  - Interrupt structure change?
  - New trap type?
  - Be concrete where possible. -->

## Measured impact (if applicable)

<!-- Numbers from the simulator:
  - Latency differences
  - Allocation patterns
  - Mailbox occupancy histograms
  - Context sizes -->

## Affected CFPU phase

<!-- Which phase would this land in?
  - [ ] F2 (RTL single Nano core)
  - [ ] F3 (Tiny Tapeout silicon)
  - [ ] F4 (multi-core FPGA)
  - [ ] F5 (Rich core + heterogeneous)
  - [ ] F6 (distributed multi-board / silicon)
  - [ ] Unclear — needs discussion -->

## Cross-references

<!-- Link the corresponding issue in FenySoft/CLI-CPU once opened. -->

- CLI-CPU issue: #TODO
- Related Symphact code: `src/...`
- Related test: `tests/...`
