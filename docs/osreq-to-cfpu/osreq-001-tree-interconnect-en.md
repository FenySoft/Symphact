# OSREQ-001: Tree-structured interconnect between cores

> Magyar verzió: [osreq-001-tree-interconnect-hu.md](osreq-001-tree-interconnect-hu.md)

> **Status:** Draft — awaiting hardware feedback
>
> **Affected CFPU phase:** F4 (multi-core FPGA), F5 (heterogeneous), F6 (silicon)

## Summary

The Symphact supervisor tree, capability delegation model, and memory management assume a **tree-structured hierarchy**. The interconnect topology must mirror this tree — not a flat bus, not a uniform mesh, but a **hierarchical tree (fat tree)**.

## Context — why a tree?

### 1. The software is naturally a tree

The Symphact supervisor tree is a tree:

```
TRootSupervisor [Admin]              ← Rich Core
├── TKernelCoreSupervisor
│   ├── TScheduler
│   ├── TRouter
│   └── TCapabilityRegistry
└── TKernelIoSupervisor
    └── Device actors...
        └── App actors...
            └── Nano Core actors...
```

Capability delegation is a tree: the root supervisor is the only Admin, and rights **propagate downward** through delegation. Error handling (supervision) is also a tree: a failing actor is handled by its **parent**.

### 2. Physical memory is a tree

On the CFPU there is no shared memory. Every core has its own private SRAM:

```
                         Chip
                          │
                   ┌──────┴──────┐
              Seal Core     Rich Core(s)        Level 0: privileged
                              │
               ┌──────────────┼──────────────┐
           Cluster 0      Cluster 1      Cluster K    Level 1: Nano clusters
            /  |  \        /  |  \        /  |  \
          N0  N1  N2     N3  N4  N5     N6  N7  N8   Level 2: Nano cores
          │   │   │      │   │   │      │   │   │
         4KB 4KB 4KB    4KB 4KB 4KB    4KB 4KB 4KB   Level 3: private SRAM
```

"Data sharing" is only possible through mailbox messages, and the message path traverses **up and down the tree**.

### 3. Message routing path

When `N2` (Cluster 0) sends a message to `N5` (Cluster 1):

```
N2 [own SRAM]
  → N2 outbox FIFO (HW)
    → Cluster 0 router (up)                    1 hop
      → Chip-level router (Rich Core level)    2 hop
        → Cluster 1 router (down)              3 hop
          → N5 inbox FIFO (HW)                 4 hop
            → N5 [copied into own SRAM]
```

Within the same cluster (N0 → N2): **1 hop** (stays within the cluster router).

### 4. Multi-chip: the tree grows further

```
              Fabric (multi-chip)
             /         |         \
         Chip 0     Chip 1     Chip 2       inter-chip link
           │          │          │
        Rich Core  Rich Core  Rich Core
        /  |  \    /  |  \    /  |  \
       Cl0 Cl1..  Cl0 Cl1..  Cl0 Cl1..
```

The `TActorRef` format (`[chip-id:8][core-id:16][offset:16][HMAC:16][perms:8]`) encodes this tree addressing. The router resolves the target by traversing up and down the hierarchy.

## Current software workaround

In the .NET reference implementation (`TActorSystem`), there is no topology awareness — all actors live in a flat dictionary and `Send()` does a direct lookup. This does not scale to the CFPU.

## Proposed hardware behaviour

### Introducing the Cluster concept

| Element | Description |
|---------|-------------|
| **Cluster** | Physical group of 4-16 Nano cores with a local router |
| **Cluster router** | Direct forwarding of intra-cluster messages (1 hop, ~2-4 cycles) |
| **Chip router** | Inter-cluster message forwarding (2+ hops) |
| **Inter-chip bridge** | Inter-chip messages (SPI/LVDS link) |

### Routing hierarchy

```
Level 0: Intra-cluster    N→N same cluster          1 hop    ~2-4 cycles
Level 1: Inter-cluster    N→N different cluster      2 hops   ~6-10 cycles
Level 2: N→Rich / Rich→N  Supervisor communication   1-2 hops ~4-8 cycles
Level 3: Inter-chip        Chip→Chip                 3+ hops  ~50-200 cycles
```

### MMIO registers (proposal)

| Address | Name | R/W | Description |
|---------|------|-----|-------------|
| `0xF0000100` | `NANO_CORE_COUNT` | R/O | Total number of Nano cores |
| `0xF0000104` | `RICH_CORE_COUNT` | R/O | Number of Rich cores |
| `0xF0000108` | `CLUSTER_COUNT` | R/O | Number of clusters |
| `0xF000010C` | `CORES_PER_CLUSTER` | R/O | Cores per cluster |
| `0xF0000110` | `CLUSTER_ID` | R/O | Which cluster this core belongs to |
| `0xF0000114` | `CHIP_ID` | R/O | Multi-chip: which chip |

### Routing table

The chip router does **not need a flat routing table** for every core. Instead, hierarchical addressing:

```
Destination: [chip-id].[cluster-id].[core-offset]

Routing decision:
  if chip-id ≠ own → inter-chip bridge
  if cluster-id ≠ own → chip router → target cluster router
  if cluster-id = own → cluster router (local delivery)
```

This is O(1) routing, not O(N) lookup.

## Why tree and not mesh?

| Topology | Advantage | Disadvantage for CFPU |
|----------|-----------|----------------------|
| **Flat bus** | Simple (F4 level) | O(N) contention — unusable at 16+ cores |
| **Crossbar** | O(1) latency | O(N²) wiring — physically impossible at 10k cores |
| **2D Mesh** | Good for GPUs (uniform workload) | Does not match supervisor hierarchy; uniform routing is wasteful when communication is local |
| **Tree (fat tree)** | O(log N) wiring; natural hierarchy | Root bottleneck — but fat tree mitigates it, and the supervisor tree is a tree anyway |

## Open questions (HW feedback needed)

1. **Cluster size** — how many Nano cores per cluster? (4? 8? 16?)
2. **Fat tree width** — how wide should the bus be toward the root (chip router)?
3. **Rich Core position** — at the tree root (all messages pass through it?) or beside it (dedicated uplink)?
4. **Inter-chip link** — SPI? LVDS? How many pins? Max message size?
5. **Cluster router** — programmable (microcode-configurable routing) or fixed logic?
6. **Broadcast/multicast** — HW multicast support for supervisor restart scenarios?

## Cross-references

- CLI-CPU issue: #TODO (osreq-from-os label)
- Symphact roadmap: M2.3 (Router), M2.4 (Memory Manager)
- Boot sequence: `docs/boot-sequence-hu.md` — step 8 (Nano Core Wake)
- CLI-CPU architecture: `docs/architecture-hu.md` — "Scaling to F6" section
