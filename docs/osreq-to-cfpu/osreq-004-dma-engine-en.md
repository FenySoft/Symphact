# OSREQ-004: DMA engine — non-blocking persistence support

> Magyar verzió: [osreq-004-dma-engine-hu.md](osreq-004-dma-engine-hu.md)

> **Status:** Draft — awaiting hardware feedback
>
> **Affected CFPU phase:** F5 (heterogeneous), F6 (silicon)

## Summary

The Symphact persistence model (M0.5 — Event Sourcing) requires that an actor's state can be written to external storage (DRAM/Flash/FRAM) in an **asynchronous, non-blocking manner**. Since core SRAM is volatile (lost on power failure), a **DMA engine** is needed for journal/snapshot writes so that core execution is not blocked.

## Context

The actor persistence model:

```
1. Actor processes a message → new state
2. Journal entry (message + new state hash) → write to external storage
3. Periodically: snapshot (full state) → external storage
4. After power loss: snapshot + journal replay → state recovery
```

Steps 2 and 3 **must not block** actor message processing — if it waits for a synchronous write, the mailbox FIFO fills up and backpressure occurs.

## Proposed hardware behavior

### DMA architecture options

| Option | Description | Advantage | Disadvantage |
|--------|-------------|-----------|--------------|
| **A) Per-core DMA** | Each core has its own DMA channel | No contention | Expensive (area, wiring) |
| **B) Central DMA controller** | A shared DMA with priority scheduling | Cheaper | Contention at 10k+ cores |
| **C) Per-cluster DMA** | One DMA per cluster (OSREQ-001 tree topology) | Compromise | Cluster size dependent |

**Recommended: C) Per-cluster DMA** — aligns with the OSREQ-001 tree topology.

### DMA registers (proposal)

| Address | Name | R/W | Description |
|---------|------|-----|-------------|
| `0xF0000900 + cluster_id×16` | `DMA_SRC_ADDR` | R/W | Source address (in core SRAM) |
| `0xF0000904 + cluster_id×16` | `DMA_DST_ADDR` | R/W | Destination address (external FRAM/PSRAM) |
| `0xF0000908 + cluster_id×16` | `DMA_LENGTH` | R/W | Transfer size (in bytes) |
| `0xF000090C + cluster_id×16` | `DMA_CONTROL` | R/W | `1` = start, bit 1 = interrupt on complete |

### DMA flow

```
1. Actor handler completes → journal entry in SRAM
2. SW: DMA_SRC = SRAM journal buffer address
3. SW: DMA_DST = FRAM/PSRAM destination address  
4. SW: DMA_LENGTH = entry size
5. SW: DMA_CONTROL = start + IRQ
6. Core continues message processing (does not wait!)
7. HW: DMA copies data in the background
8. HW: DMA complete → interrupt to core
9. SW: journal buffer release
```

## Target devices (related to OSREQ-002)

The CLI-CPU architecture doc defines three-tier storage:

| Device | Type | Speed | Usage |
|--------|------|-------|-------|
| OPI PSRAM | Volatile, large | ~50 MHz | Heap overflow, large state |
| OPI FRAM | Non-volatile, medium | ~50 MHz | **Journal, snapshot** |
| OPI Flash | Non-volatile, slow | ~50 MHz (read) | Code, read-only data |

The primary target device for persistence is **FRAM** — non-volatile, fast writes, no wear leveling.

## Open questions (HW feedback needed)

1. **Per-core vs per-cluster vs central DMA** — which fits within the area budget?
2. **DMA and mailbox priority** — does DMA use the same bus as the mailbox? If so, what priority?
3. **Scatter-gather** — is writing non-contiguous SRAM regions with a single DMA transfer needed?
4. **Max transfer size** — is snapshot size limited? (Nano SRAM 4 KB → max 4 KB)
5. **Double buffering** — are two journal buffers (ping-pong) needed in core SRAM, or is one sufficient?

## Cross-references

- Symphact roadmap: M0.5 (Persistence / Event Sourcing)
- CLI-CPU architecture: OPI bus, FRAM section
- OSREQ-001: cluster topology (per-cluster DMA alignment)
- OSREQ-002: MMIO map (DMA register addresses)
