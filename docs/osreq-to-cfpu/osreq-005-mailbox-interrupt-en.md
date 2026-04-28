# OSREQ-005: Mailbox interrupt vs polling — core notification mechanism

> Magyar verzió: [osreq-005-mailbox-interrupt-hu.md](osreq-005-mailbox-interrupt-hu.md)

> Version: 1.0

> **Status:** Draft — awaiting hardware feedback
>
> **Affected CFPU phase:** F4 (multi-core FPGA), F5 (heterogeneous), F6 (silicon)

## Summary

The Symphact scheduler (M0.4) needs to know how a core is **notified when a new message arrives in its mailbox FIFO**. This is a fundamental question: **hardware interrupt** or **software polling**? The answer affects power consumption, latency, and the scheduler algorithm.

## Context

According to the CFPU event-driven model, a core **sleeps** until a message arrives. But the mechanics of this are not specified:

```
Message arrives in N5 mailbox FIFO
  → ??? → N5 core wakes up and processes it
```

The `???` is the question.

## Options

### A) Hardware interrupt (recommended)

```
1. Message → N5 inbox FIFO (HW write)
2. HW: inbox not-empty → IRQ line active
3. HW: core wake (if sleeping) or IRQ handler call (if running)
4. SW: IRQ handler → TryReceive() → actor handler
```

| Advantage | Disadvantage |
|-----------|--------------|
| Zero-latency wake (1-2 cycles) | IRQ line required per core |
| Minimal power consumption (core truly sleeps) | IRQ controller area cost |
| Deterministic latency | Edge case: IRQ storm (many messages at once) |

### B) Software polling

```
1. Message → N5 inbox FIFO (HW write)
2. SW: core periodically polls the inbox status register
3. SW: if not-empty → TryReceive() → actor handler
```

| Advantage | Disadvantage |
|-----------|--------------|
| Simpler HW (no IRQ line) | Core cannot sleep → continuous power consumption |
| No IRQ overhead | Polling latency (worst case = poll interval) |
| | CPU cycle waste on empty polls |

### C) Hybrid (interrupt + coalescing)

```
1. Message → inbox FIFO
2. HW: N messages accumulate OR T cycles elapse → single IRQ
3. SW: batch receive → actor handler
```

| Advantage | Disadvantage |
|-----------|--------------|
| Reduces IRQ storm | More complex HW (counter + timer) |
| Better throughput under heavy load | Increases worst-case latency |

## Recommended solution

**A) Hardware interrupt** for Nano cores, **C) Hybrid** for the Rich Core:

- **Nano cores** are simple — one actor, one mailbox, less frequent messages → pure interrupt is the most efficient
- **Rich Core** runs many kernel actors time-sliced → interrupt coalescing reduces overhead

### Recommended registers

| Address | Name | R/W | Description |
|---------|------|-----|-------------|
| `0xF0000600` | `IRQ_MAILBOX_HANDLER` | R/W | Mailbox not-empty IRQ handler address |
| `0xF0000610 + core_id×4` | `MAILBOX_IRQ_ENABLE[n]` | R/W | Per-core mailbox IRQ enable |
| `0xF0000620` | `IRQ_COALESCE_COUNT` | R/W | Rich Core: number of messages before IRQ (0 = on every message) |
| `0xF0000624` | `IRQ_COALESCE_TIMEOUT` | R/W | Rich Core: max wait in cycles (fallback timer) |

### Wake mechanism

| Core state | Mailbox message arrives | Behavior |
|------------|------------------------|----------|
| **Sleeping** | inbox IRQ → **wake** | Core wakes up, PC = IRQ handler |
| **Running** | inbox IRQ → **pending** | IRQ handler is called after the current instruction boundary |
| **Reset** | inbox IRQ → **dropped** | Core has not woken up, message remains in FIFO |

## Power consumption impact

| Model | 10k Nano cores, avg 1% active | Consumption |
|-------|-------------------------------|-------------|
| Polling (all running) | 10k × ~100 µW | ~1 W |
| Interrupt (99% sleeping) | 100 × 100 µW + 9900 × ~1 µW | ~20 mW |

**50× difference** — this is one of the CFPU's key value propositions (ultra-low power).

## Open questions (HW feedback needed)

1. **IRQ line topology** — per-core dedicated IRQ line, or cluster-level multiplexed?
2. **Nested IRQ** — if a mailbox IRQ handler is running and another message arrives, nested IRQ or pending?
3. **Wake latency** — how many cycles for the sleep → running transition? (target: ≤5 cycles)
4. **IRQ priority** — mailbox vs watchdog vs trap — fixed priority or programmable?
5. **Power domain** — is the sleeping core clock-gated, power-gated, or both?

## Cross-references

- Symphact roadmap: M0.4 (Scheduler / Per-Actor Parallelism)
- CLI-CPU architecture: "Event-driven, not clock-driven" section, Sleep/Wake logic
- OSREQ-002: MMIO map (IRQ registers)


---

## Changelog

| Version | Date | Summary |
|---------|------|---------|
| 1.0 | 2026-04-20 | Initial release |
