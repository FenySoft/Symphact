# OSREQ-002: MMIO Memory Map — OS↔HW Register Interface

> Magyar verzió: [osreq-002-mmio-memory-map-hu.md](osreq-002-mmio-memory-map-hu.md)

> **Status:** Draft — awaiting hardware feedback
>
> **Affected CFPU phase:** F4 (multi-core FPGA), F5 (heterogeneous), F6 (silicon)

## Summary

The Symphact boot process assumes specific MMIO registers for communication between the Rich Core and the Seal Core, core discovery, mailbox management, and interrupt control. These are the OS↔HW interface contracts — the CLI-CPU HW design must implement these addresses and semantics (or propose alternatives).

## Context

`docs/boot-sequence-hu.md` uses specific MMIO addresses at every step of the boot process. These are not arbitrary — they follow from the logical order of the boot sequence. If the HW chooses different addresses or mechanisms, the OS boot code must be updated.

## Proposed MMIO Map

### Seal Core → Rich Core Communication (`0xF0002xxx`)

| Address | Name | R/W | Size | Description |
|---------|------|-----|------|-------------|
| `0xF0002000` | `EFUSE_ROOT_HASH` | R/O | 32 byte | BitIce Foundation Root CA SHA-256 hash — burned at manufacturing, IMMUTABLE |
| `0xF0002020` | `SEAL_CORE_STATUS` | R/O | 4 byte | Self-test result, verify result, heartbeat counter |
| `0xF0002024` | `SEAL_CORE_SIGNAL` | R/O | 4 byte | `0` = wait, `1` = verified + go, `2` = fail + halt |
| `0xF0002030` | `QRAM_CODE_BASE` | R/O | 4 byte | Start address of verified CIL binary (in Quench-RAM) |
| `0xF0002034` | `QRAM_CODE_SIZE` | R/O | 4 byte | Size of verified CIL binary in bytes |

### Core Discovery (`0xF0000100`)

| Address | Name | R/W | Size | Description |
|---------|------|-----|------|-------------|
| `0xF0000100` | `NANO_CORE_COUNT` | R/O | 4 byte | Total number of Nano cores |
| `0xF0000104` | `RICH_CORE_COUNT` | R/O | 4 byte | Number of Rich cores |
| `0xF0000108` | `CLUSTER_COUNT` | R/O | 4 byte | Number of clusters (→ OSREQ-001) |
| `0xF000010C` | `CORES_PER_CLUSTER` | R/O | 4 byte | Cores per cluster (→ OSREQ-001) |
| `0xF0000110` | `CLUSTER_ID` | R/O | 4 byte | Which cluster the given core belongs to |
| `0xF0000114` | `CHIP_ID` | R/O | 4 byte | Multi-chip identifier |

### Mailbox Management (`0xF0000200–0xF00003xx`)

| Address | Name | R/W | Size | Description |
|---------|------|-----|------|-------------|
| `0xF0000200` | `MAILBOX_BASE_ADDR` | R/O | 4 byte | Base address of mailbox FIFOs |
| `0xF0000300 + core_id×4` | `MAILBOX_ENABLE[n]` | R/W | 4 byte | Per-core mailbox FIFO enable (`1` = active) |

### Per-core Status (`0xF0000400`)

| Address | Name | R/W | Size | Description |
|---------|------|-----|------|-------------|
| `0xF0000400 + core_id×4` | `CORE_STATUS[n]` | R/O | 4 byte | `0` = Sleeping, `1` = Running, `2` = Error, `3` = Reset |

### Interrupt Controller (`0xF0000600`)

| Address | Name | R/W | Size | Description |
|---------|------|-----|------|-------------|
| `0xF0000600` | `IRQ_MAILBOX_HANDLER` | R/W | 4 byte | Mailbox not-empty interrupt handler address |
| `0xF0000604` | `IRQ_WATCHDOG_HANDLER` | R/W | 4 byte | Watchdog interrupt handler address |
| `0xF0000608` | `IRQ_TRAP_HANDLER` | R/W | 4 byte | Nano core trap interrupt handler address |

### Mailbox Address Table (`0xF0000800`)

| Address | Name | R/W | Size | Description |
|---------|------|-----|------|-------------|
| `0xF0000800 + core_id×4` | `MAILBOX_ADDR[n]` | R/O | 4 byte | Core-id → mailbox FIFO physical address mapping |

### QSPI Flash Controller (`0xF0001000`)

| Address | Name | R/W | Size | Description |
|---------|------|-----|------|-------------|
| `0xF0001000` | `QSPI_CONFIG` | R/W | 4 byte | QSPI configuration |
| `0xF0001004` | `QSPI_FLASH_ADDR` | R/W | 4 byte | Flash address (read position) |
| `0xF0001008` | `QSPI_BINARY_SIZE` | R/O | 4 byte | Binary size |
| `0xF000100C` | `QSPI_DATA` | R/O | 1 byte | Read byte |

## MMIO Map Overview

```
0xF0000100 ┬─ Core discovery (6 registers)
0xF0000200 ┼─ Mailbox base address
0xF0000300 ┼─ Per-core mailbox enable (N registers)
0xF0000400 ┼─ Per-core status (N registers)
0xF0000600 ┼─ Interrupt controller (3 vectors)
0xF0000800 ┼─ Mailbox address table (N registers)
0xF0001000 ┼─ QSPI Flash controller (4 registers)
0xF0002000 ┴─ Seal Core interface (5 registers)
```

## Boot Sequence MMIO Access Order

```
1. [Seal Core]  Writes  0xF0002024 = 1 (verified + go)
2. [Rich Core]  Reads   0xF0002024 → wait until = 1
3. [Rich Core]  Reads   0xF0002030, 0xF0002034 → CIL binary location
4. [Rich Core]  Reads   0xF0000100, 0xF0000104 → core counts
5. [Rich Core]  Reads   0xF0000200 → mailbox base
6. [Rich Core]  Writes  0xF0000600..608 → interrupt handlers
7. [Rich Core]  Writes  0xF0000300 + rich_core_id*4 → own mailbox enable
8. [Rich Core]  Reads   0xF0000400..N → core status (all Sleeping)
9. [Rich Core]  Reads   0xF0000800..N → mailbox address table
```

## Current Software Implementation

In the .NET reference implementation there is no MMIO — `TActorSystem` manages internal state directly. The boot sequence code (`src/Symphact.Boot/`) does not exist yet.

## Open Questions (HW feedback needed)

1. **Address range** — is the `0xF0000000–0xF000FFFF` range acceptable for MMIO regions, or does the HW prefer a different layout?
2. **Register width** — 32-bit (4 byte) everywhere, or are there cases where 8/16 bit would suffice?
3. **Atomic read** — should `SEAL_CORE_SIGNAL` be polled, or is there an interrupt as well?
4. **Max core count** — how far does the `core_id×4` offset scale? (F6: 16 Nano + 6 Rich = 22 → ~88 bytes; 10k cores → ~40 KB)
5. **QSPI vs OPI** — the architecture doc mentions OPI for F6; should QSPI registers be modified for OPI?

## Cross-references

- Symphact boot sequence: `docs/boot-sequence-hu.md` — steps 1–8
- CLI-CPU architecture: `docs/architecture-hu.md` — memory map section
- OSREQ-001: tree topology (cluster registers: `0xF0000108–0xF0000114`)
