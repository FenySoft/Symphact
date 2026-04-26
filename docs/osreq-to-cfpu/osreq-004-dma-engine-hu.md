# OSREQ-004: DMA engine — nem-blokkoló persistence támogatás

> English version: [osreq-004-dma-engine-en.md](osreq-004-dma-engine-en.md)

> **Állapot:** Draft — hardveres visszajelzésre vár
>
> **Érintett CFPU fázis:** F5 (heterogén), F6 (silicon)

## Összefoglaló

A Symphact persistence modellje (M0.5 — Event Sourcing) megköveteli, hogy egy actor állapotát **aszinkron, nem-blokkoló módon** lehessen kiírni külső tárolóra (DRAM/Flash/FRAM). Mivel a core SRAM volatile (tápelvesztésnél elveszik), a journal/snapshot kiíráshoz **DMA engine** szükséges, ami a core futását nem blokkolja.

## Kontextus

Az actor persistence modellje:

```
1. Actor feldolgoz egy üzenetet → új state
2. Journal entry (üzenet + új state hash) → kiírás külső tárolóra
3. Periodikusan: snapshot (teljes state) → külső tároló
4. Tápelvesztés után: snapshot + journal replay → state visszaállítás
```

A 2. és 3. lépés **nem blokkolhatja** az actor üzenet-feldolgozását — ha szinkron kiírásra vár, a mailbox FIFO megtelik és backpressure lép fel.

## Javasolt hardveres viselkedés

### DMA architektúra opciók

| Opció | Leírás | Előny | Hátrány |
|-------|--------|-------|---------|
| **A) Per-core DMA** | Minden core saját DMA csatornával | Nincs contention | Drága (terület, vezeték) |
| **B) Központi DMA controller** | Egy megosztott DMA, prioritásos ütemezéssel | Olcsóbb | Contention 10k+ core-nál |
| **C) Per-cluster DMA** | Cluster-enként egy DMA (OSREQ-001 fa topológiával) | Kompromisszum | Cluster méretfüggő |

**Javasolt: C) Per-cluster DMA** — illeszkedik az OSREQ-001 fa topológiához.

### DMA regiszterek (javaslat)

| Cím | Név | R/W | Leírás |
|-----|-----|-----|--------|
| `0xF0000900 + cluster_id×16` | `DMA_SRC_ADDR` | R/W | Forrás cím (core SRAM-ban) |
| `0xF0000904 + cluster_id×16` | `DMA_DST_ADDR` | R/W | Cél cím (külső FRAM/PSRAM) |
| `0xF0000908 + cluster_id×16` | `DMA_LENGTH` | R/W | Átvitel mérete (bájtokban) |
| `0xF000090C + cluster_id×16` | `DMA_CONTROL` | R/W | `1` = start, bit 1 = interrupt on complete |

### DMA flow

```
1. Actor handler kész → journal entry a SRAM-ban
2. SW: DMA_SRC = SRAM journal buffer cím
3. SW: DMA_DST = FRAM/PSRAM cél cím  
4. SW: DMA_LENGTH = entry méret
5. SW: DMA_CONTROL = start + IRQ
6. Core folytatja az üzenet-feldolgozást (nem vár!)
7. HW: DMA háttérben másolja az adatot
8. HW: DMA kész → interrupt a core-nak
9. SW: journal buffer felszabadítás
```

## Céleszközök (OSREQ-002-vel összefüggésben)

A CLI-CPU architecture doc háromszintű tárolást definiál:

| Eszköz | Típus | Sebesség | Használat |
|--------|-------|----------|-----------|
| OPI PSRAM | Volatile, nagy | ~50 MHz | Heap overflow, nagy state |
| OPI FRAM | Non-volatile, közepes | ~50 MHz | **Journal, snapshot** |
| OPI Flash | Non-volatile, lassú | ~50 MHz (read) | Kód, read-only adat |

A persistence elsődleges céleszköze a **FRAM** — nem-volatile, gyors írás, nincs wear leveling.

## Nyitott kérdések (HW visszajelzés szükséges)

1. **Per-core vs per-cluster vs központi DMA** — melyik illeszkedik a terület-budgetbe?
2. **DMA és mailbox prioritás** — a DMA használja-e ugyanazt a buszt, mint a mailbox? Ha igen, prioritás?
3. **Scatter-gather** — kell-e nem-összefüggő SRAM régiók egyetlen DMA-val való kiírása?
4. **Max átvitel méret** — snapshot méret korlátozva van? (Nano SRAM 4 KB → max 4 KB)
5. **Double buffering** — a core SRAM-ban kell-e két journal buffer (ping-pong), vagy elég egy?

## Kereszthivatkozások

- Symphact roadmap: M0.5 (Persistence / Event Sourcing)
- CLI-CPU architecture: OPI busz, FRAM szekció
- OSREQ-001: cluster topológia (per-cluster DMA illeszkedés)
- OSREQ-002: MMIO térkép (DMA regiszter címek)
