# OSREQ-003: Core reset mechanism — supervisor restart support

> Magyar verzió: [osreq-003-core-reset-hu.md](osreq-003-core-reset-hu.md)

> Version: 1.0

> **Status:** Draft — awaiting hardware feedback
>
> **Affected CFPU phase:** F4 (multi-core FPGA), F5 (heterogeneous), F6 (silicon)

## Summary

The Symphact **"let it crash"** model requires that a supervisor actor can restart a faulty child actor's core. This requires **hardware core reset** that atomically clears the core's SRAM and mailbox FIFO, then brings the core into a restartable state.

## Context

The Erlang/OTP supervision model is the foundation of Symphact (M0.3 milestone). When an actor throws an error:

```
1. Actor crash (e.g. invalid message, assertion failure)
2. Trap → supervisor notification (parent actor)
3. Supervisor decides: Restart / Stop / Escalate
4. On Restart: core reset → new actor state → resume
```

**Restart** is the most common strategy. On CFPU this means: the Nano/Rich core **resets to a clean state via hardware**.

## Proposed hardware behavior

### Core Reset register

| Address | Name | R/W | Size | Description |
|---------|------|-----|------|-------------|
| `0xF0000500 + core_id×4` | `CORE_RESET[n]` | W/O | 4 byte | Writing `1` triggers the core reset |

### Reset sequence (HW)

```
1. CORE_RESET[n] = 1 write (by Rich Core / supervisor)
2. HW: target core execution HALT (immediate, does not wait for message boundary)
3. HW: target core SRAM → zero-fill (full, including stack + heap + locals)
4. HW: target core mailbox FIFO → flush (all pending messages discarded)
5. HW: target core PC = 0 (or boot entry point)
6. HW: CORE_STATUS[n] = 3 (Reset)
7. HW: target core remains in HALT state — the scheduler/supervisor decides on restart
```

### Restart (SW)

```
8. SW: Scheduler loads new CIL code into core SRAM (from flash or Rich Core)
9. SW: Mailbox ENABLE[n] = 1
10. SW: Core wake signal (→ CORE_STATUS[n] = 1, Running)
```

## Why is HW support needed?

| Approach | Problem |
|----------|---------|
| **SW-only reset** (Rich Core overwrites Nano SRAM) | The Rich Core **cannot see** the Nano core's SRAM (shared-nothing!) |
| **Self-reset** (the Nano core clears itself) | If the core has crashed, it cannot be trusted |
| **HW reset line** | ✅ Reliable, atomic, independent of the core |

## Security considerations

- **Only the supervisor core** (Rich Core) may write the `CORE_RESET[n]` register — capability check required
- A Nano core **cannot reset** itself or another Nano core — only the Rich Core (supervisor) can
- The reset **must not clear** the Seal Core — it is hardwired, immutable
- **Mailbox flush** is important: poisoned messages must not remain in the FIFO

## Estimated performance impact

| Operation | SW-only (if possible) | HW reset |
|-----------|-----------------------|----------|
| SRAM clear (4 KB Nano) | ~1000 cycles (per byte) | ~10-50 cycles (HW zero-fill) |
| Mailbox flush | ~64 cycles (per slot) | ~1-2 cycles (pointer reset) |
| Full restart | ~2000 cycles | ~100 cycles |

In the **let-it-crash** model, restart is a **frequent, normal operation** — not an exception. Therefore latency matters.

## Open questions (HW feedback needed)

1. **Partial reset** — is partial SRAM clear needed (e.g. heap only, stack preserved)? Or always full wipe?
2. **Mailbox drain vs flush** — should pending messages be discarded (flush), or readable by the supervisor (drain)?
3. **Reset time guarantee** — is the reset time deterministic? (important for real-time use cases)
4. **Cascade reset** — if a cluster supervisor is reset, does the entire cluster reset? Or per-core?
5. **Reset reason register** — besides CORE_STATUS, is a `CORE_RESET_REASON` register needed (trap code, watchdog, explicit reset)?

## Cross-references

- Symphact roadmap: M0.3 (Supervision / Let-it-crash)
- Boot sequence: `docs/boot-sequence-hu.md` — step 1 (POR)
- CLI-CPU architecture: Seal Core self-test analogy


---

## Changelog

| Version | Date | Summary |
|---------|------|---------|
| 1.0 | 2026-04-20 | Initial release |
