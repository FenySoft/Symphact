# OSREQ-006: Inter-chip link protocol — distributed fabric communication

> Magyar verzió: [osreq-006-interchip-link-hu.md](osreq-006-interchip-link-hu.md)

> Version: 1.1

> **Status:** Draft — awaiting hardware feedback
>
> **Affected CFPU phase:** F6 (silicon), F7 (production)

## Summary

The Symphact distribution model (M0.6 — Remoting) requires that **multiple CFPU chips** operate as a single logical fabric. This requires a standardized, low-latency **inter-chip link** through which actor mailbox messages pass — while preserving location transparency.

## Context

The fundamental principle of Symphact: **`TActorRef` does not reveal whether the target actor is local, on another core, or on another chip**. This is location transparency — the same `Send(ref, msg)` call regardless of where the target is. But for this, the HW must route transparently across chip boundaries.

The OSREQ-001 tree topology extends to multi-chip level:

```
              Fabric
             /   |   \
         Chip 0  Chip 1  Chip 2
           │       │       │
        Rich     Rich    Rich
        / | \   / | \   / | \
       Cl  Cl  Cl  Cl  Cl  Cl
```

The inter-chip link sits **above the tree root** — this is the slowest but least frequent path.

## Proposed hardware behavior

### Physical layer options

| Option | Speed | Pin count | Distance | Notes |
|--------|-------|-----------|----------|-------|
| **SPI** | ~10-50 Mbps | 4 (MOSI/MISO/CLK/CS) | ~10 cm | Simple but slow |
| **LVDS** | ~100-800 Mbps | 4 (2 differential pairs) | ~1 m | Fast, low EMI |
| **UART** | ~1-10 Mbps | 2 (TX/RX) | ~1 m | Very simple, very slow |
| **Custom mailbox bridge** | ~50-200 Mbps | 4-8 | ~10 cm | Referenced in the CLI-CPU architecture doc |

The architecture doc mentions `Mailbox bridge (inter-chip): 4 pin` — this is likely an SPI-like or custom protocol.

### Message format

The inter-chip message must contain:

```
┌─────────────────────────────────────────────────────┐
│ Header (fixed size)                                  │
├──────────┬──────────┬──────────┬──────────┬─────────┤
│ src_chip │ dst_chip │ dst_cluster │ dst_core │ msg_len │
│ 8 bit    │ 8 bit    │ 8 bit       │ 16 bit   │ 16 bit  │
├──────────┴──────────┴──────────┴──────────┴─────────┤
│ Payload (variable size, max ??? bytes)               │
├─────────────────────────────────────────────────────┤
│ CRC-16 (optional, link integrity)                    │
└─────────────────────────────────────────────────────┘
```

### Routing decision (related to OSREQ-001)

`TActorRef` remains chip-local (`actor-ref-scaling-en.md` §1.c/§3.c) — it does not carry a chip-id field. Addressing beyond the chip boundary uses the **proxy-actor pattern**: the sender sends to a local `remote_proxy` actor (a regular, chip-local CST index), and the proxy's internal state holds the actual target chip/cluster/core data.

```
Send(ref, msg):                                ← ref: local, chip-local CST index
  if ref points to an application actor  → intra-chip routing (OSREQ-001 tree)
  if ref points to a remote_proxy actor   → proxy actor:
    1. Message serialize (SRAM → link buffer)
    2. Add header from the proxy's internal state (src/dst chip+cluster+core)
    3. Link transmit (HW)
    4. Target chip: link receive → deserialize → dst mailbox FIFO
```

### Flow control

| Problem | Solution |
|---------|----------|
| Link slower than mailbox | **Backpressure**: link buffer full → Send blocks or NAK |
| Chip offline (power loss) | **Timeout + supervisor notification** → actor migration |
| Link error (CRC mismatch) | **Retry** (max 3×) → if unsuccessful, link down → supervisor |

## Impact on Symphact

| OS component | Change |
|-------------|--------|
| `TActorRef` | ~~Add `chip-id` field~~ — **REJECTED**: per the `actor-ref-scaling-en.md` §3.b decision, `TActorRef` remains chip-local (32-bit opaque CST index); addressing beyond the chip boundary uses the proxy-actor pattern (§3.c) |
| `TRouter` | Inter-chip routing: chip-id lookup → link buffer |
| `TSerializer` | Message serialize/deserialize for inter-chip transfer |
| `TDistributionSupervisor` | Link health monitoring, actor migration |

## Open questions (HW feedback needed)

1. **Link type** — SPI, LVDS, custom? Pin budget?
2. **Max message size** — fixed (e.g. 64 bytes) or variable? If fixed → fragmentation for large messages?
3. **Multi-chip topology** — point-to-point (daisy chain), star (hub), or tree (OSREQ-001 extension)?
4. **Link speed** — target Mbps? Latency budget (target: <10 µs per message)?
5. **Hot plug** — is runtime chip add/remove support needed?
6. **Encryption** — are inter-chip messages encrypted? (protection against physical access)

## Cross-references

- Symphact roadmap: M0.6 (Remoting / Distribution)
- CLI-CPU architecture: `Mailbox bridge (inter-chip): 4 pin`, pin budget table
- OSREQ-001: tree topology (multi-chip extension)
- OSREQ-002: MMIO map (`CHIP_ID` register)


---

## Changelog

| Version | Date | Summary |
|---------|------|---------|
| 1.1 | 2026-07-19 | "Impact on Symphact" table: `TActorRef` chip-id field proposal marked REJECTED (`actor-ref-scaling-en.md` §3.b); routing decision pseudocode fixed to the proxy-actor pattern |
| 1.0 | 2026-04-20 | Initial release |
