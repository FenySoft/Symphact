# OSREQ-001: Tree-structured interconnect between cores

> Magyar verzió: [osreq-001-tree-interconnect-hu.md](osreq-001-tree-interconnect-hu.md)

> Version: 1.2

> **Status:** Resolved — see banner below
>
> **Affected CFPU phase:** F4 (multi-core FPGA), F5 (heterogeneous), F6 (silicon)

---

> ## ✅ RESOLVED
>
> The CLI-CPU team has **decided** this topology requirement — not with the pure tree (fat tree) topology proposed below, but with a **4-level hierarchical mesh + crossbar** architecture that satisfies the same underlying need (locality-aware, hierarchical, O(1)-routing interconnect aligned with the supervisor tree). Canonical, authoritative specification: [CLI-CPU/docs/interconnect-en.md](https://github.com/FenySoft/CLI-CPU/blob/main/docs/interconnect-en.md) v3.8.
>
> **The pure fat tree was explicitly excluded** by CLI-CPU (see the "Excluded alternatives" table): *"Root bottleneck and SPOF; converges to the hierarchical mesh once horizontal links are added."* — in other words, a fat tree augmented with horizontal (redundant) links converges exactly to the hierarchical mesh+crossbar construction described below.
>
> **Adopted topology:** L0 Cluster (4×4 mesh, XY dimension-order routing, wormhole switching, 16 cores) → L1 Tile (8×8 crossbar) → L2 Region (8×8 crossbar) → L3 Chip (star topology, Seal Core at the geometric center). Every L1–L3 crossbar is **always 1 deterministic hop** at its own level, using Virtual Cut-Through (VCT) switching. Arbitration: iSLIP (1 cycle/decision). Flow control: credit-based (4 credits/channel) + Virtual Output Queueing (VOQ, a separate queue per input toward every output port). Traffic isolation: 2 Virtual Networks (VN0 = control/supervisor, VN1 = actor). Deadlock freedom: XY routing (acyclic channel ordering, Dally & Seitz 1987) + VCT + VOQ + credit-based flow control.
>
> **The "Routing hierarchy" table and hop/latency estimates below (Level 0–3, ~2-4/~6-10/~4-8/~50-200 cycles) are outdated** — they assumed a 2-level (cluster/inter-cluster) model; the actual architecture has 4 levels (cluster/tile/region/chip) with a different hop structure. For the canonical hop count and cycle latency, see the "Hop count and latency summary" section in `interconnect-en.md`: neighbor core 1 hop (~5-10 cycles), cross-cluster same tile 13 hops (~39-59 cycles), cross-tile same region 15 hops (~63-109 cycles), cross-region 18 hops (~93-171 cycles). Inter-chip (multi-chip fabric) latency is an F6+ topic — it is not yet closed on the CLI-CPU side either, and remains an open question.
>
> The document below remains in its **original, Draft-era** state as an archive.

---

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

The tree addressing is encoded in the HW routing address (`[chip-id][cluster-id][core-offset]`), which the router resolves by traversing up and down the hierarchy. `TActorRef` itself remains a chip-local, opaque 32-bit CST index (see `actor-ref-scaling-en.md` §1.c) — addressing beyond the chip boundary uses the proxy-actor pattern (§3.c), not an embedded chip-id field.

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

## Impact on Symphact

| OS component | Change |
|-------------|--------|
| `TRouter` | Routing table becomes tree-based (hierarchical address lookup, not a flat hash map) |
| `TScheduler` | Actor placement takes tree locality into account (actors that communicate heavily are placed in the same cluster) |
| `TMemoryManager` | Per-core SRAM budget + cluster-level aggregation |
| `TActorRef` | Remains chip-local — 32-bit opaque CST index (`actor-ref-scaling-en.md` §1.c); addressing beyond the chip boundary uses the proxy-actor pattern (§3.c), not an extended ref |
| Zero-copy | Only within the same core — inter-core is always a mailbox copy |

## Open questions (HW feedback needed)

1. **Cluster size** — how many Nano cores per cluster? (4? 8? 16?)
2. **Fat tree width** — how wide should the bus be toward the root (chip router)?
3. **Rich Core position** — at the tree root (all messages pass through it?) or beside it (dedicated uplink)?
4. **Inter-chip link** — SPI? LVDS? How many pins? Max message size?
5. **Cluster router** — programmable (microcode-configurable routing) or fixed logic?
6. **Broadcast/multicast** — HW multicast support for supervisor restart scenarios?

## Cross-references

- **CLI-CPU interconnect (canonical, authoritative):** [`interconnect-en.md`](https://github.com/FenySoft/CLI-CPU/blob/main/docs/interconnect-en.md) v3.8 — the full specification of the 4-level hierarchical mesh + crossbar topology, routing, switching, and flow control
- CLI-CPU issue: #TODO (osreq-from-os label)
- Symphact roadmap: M2.3 (Router), M2.4 (Memory Manager)
- Boot sequence: `docs/boot-sequence-hu.md` — step 8 (Nano Core Wake)
- CLI-CPU architecture: `docs/architecture-hu.md` — "Scaling to F6" section


---

## Changelog

| Version | Date | Summary |
|---------|------|---------|
| 1.2 | 2026-07-19 | **Status Draft → Resolved.** CLI-CPU rejected the pure fat tree topology request (root bottleneck + SPOF), adopting a 4-level hierarchical mesh + crossbar architecture instead (`interconnect-en.md` v3.8) — this satisfies the underlying need (locality-aware, hierarchical, supervisor-tree-aligned, O(1) routing). Banner added at the top of the document; the body remains unchanged as an archive. Cross-reference added |
| 1.1 | 2026-07-19 | Fixed outdated HMAC-based `TActorRef` format reference to the CST model (§4 Multi-chip section); added missing "Impact on Symphact" section to restore hu/en parity, aligned with `actor-ref-scaling-en.md` v3.0 |
| 1.0 | 2026-04-20 | Initial release |
