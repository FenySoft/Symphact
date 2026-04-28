# OSREQ-002: MMIO memória térkép — OS↔HW regiszter interfész

> English version: [osreq-002-mmio-memory-map-en.md](osreq-002-mmio-memory-map-en.md)

> Version: 1.0

> **Állapot:** Draft — hardveres visszajelzésre vár
>
> **Érintett CFPU fázis:** F4 (multi-core FPGA), F5 (heterogén), F6 (silicon)

## Összefoglaló

A Symphact boot folyamata konkrét MMIO regisztereket feltételez a Rich Core és a Seal Core közötti kommunikációhoz, a core-ok felderítéséhez, a mailbox kezeléséhez és az interrupt vezérléshez. Ezek az OS↔HW interfész szerződései — a CLI-CPU HW tervezésnek ezeket a címeket és szemantikákat kell implementálnia (vagy alternatívát javasolnia).

## Kontextus

A `docs/boot-sequence-hu.md` a boot folyamat minden lépésében konkrét MMIO címeket használ. Ezek nem önkényesek — a boot szekvencia logikai sorrendjéből következnek. Ha a HW más címeket vagy mechanizmust választ, az OS boot kódot kell frissíteni.

## Javasolt MMIO térkép

### Seal Core → Rich Core kommunikáció (`0xF0002xxx`)

| Cím | Név | R/W | Méret | Leírás |
|-----|-----|-----|-------|--------|
| `0xF0002000` | `EFUSE_ROOT_HASH` | R/O | 32 byte | BitIce Foundation Root CA SHA-256 hash — gyártáskor beégetve, IMMUTABLE |
| `0xF0002020` | `SEAL_CORE_STATUS` | R/O | 4 byte | Self-test result, verify result, heartbeat counter |
| `0xF0002024` | `SEAL_CORE_SIGNAL` | R/O | 4 byte | `0` = wait, `1` = verified + go, `2` = fail + halt |
| `0xF0002030` | `QRAM_CODE_BASE` | R/O | 4 byte | Verified CIL binary kezdőcíme (Quench-RAM-ban) |
| `0xF0002034` | `QRAM_CODE_SIZE` | R/O | 4 byte | Verified CIL binary mérete bájtokban |

### Core felderítés (`0xF0000100`)

| Cím | Név | R/W | Méret | Leírás |
|-----|-----|-----|-------|--------|
| `0xF0000100` | `NANO_CORE_COUNT` | R/O | 4 byte | Nano core-ok teljes száma |
| `0xF0000104` | `RICH_CORE_COUNT` | R/O | 4 byte | Rich core-ok száma |
| `0xF0000108` | `CLUSTER_COUNT` | R/O | 4 byte | Cluster-ek száma (→ OSREQ-001) |
| `0xF000010C` | `CORES_PER_CLUSTER` | R/O | 4 byte | Core-ok száma cluster-enként (→ OSREQ-001) |
| `0xF0000110` | `CLUSTER_ID` | R/O | 4 byte | Az adott core melyik cluster-ben van |
| `0xF0000114` | `CHIP_ID` | R/O | 4 byte | Multi-chip azonosító |

### Mailbox kezelés (`0xF0000200–0xF00003xx`)

| Cím | Név | R/W | Méret | Leírás |
|-----|-----|-----|-------|--------|
| `0xF0000200` | `MAILBOX_BASE_ADDR` | R/O | 4 byte | Mailbox FIFO-k báziscíme |
| `0xF0000300 + core_id×4` | `MAILBOX_ENABLE[n]` | R/W | 4 byte | Per-core mailbox FIFO enable (`1` = aktív) |

### Per-core állapot (`0xF0000400`)

| Cím | Név | R/W | Méret | Leírás |
|-----|-----|-----|-------|--------|
| `0xF0000400 + core_id×4` | `CORE_STATUS[n]` | R/O | 4 byte | `0` = Sleeping, `1` = Running, `2` = Error, `3` = Reset |

### Interrupt vezérlő (`0xF0000600`)

| Cím | Név | R/W | Méret | Leírás |
|-----|-----|-----|-------|--------|
| `0xF0000600` | `IRQ_MAILBOX_HANDLER` | R/W | 4 byte | Mailbox not-empty interrupt handler cím |
| `0xF0000604` | `IRQ_WATCHDOG_HANDLER` | R/W | 4 byte | Watchdog interrupt handler cím |
| `0xF0000608` | `IRQ_TRAP_HANDLER` | R/W | 4 byte | Nano core trap interrupt handler cím |

### Mailbox address table (`0xF0000800`)

| Cím | Név | R/W | Méret | Leírás |
|-----|-----|-----|-------|--------|
| `0xF0000800 + core_id×4` | `MAILBOX_ADDR[n]` | R/O | 4 byte | Core-id → mailbox FIFO fizikai cím mapping |

### QSPI Flash controller (`0xF0001000`)

| Cím | Név | R/W | Méret | Leírás |
|-----|-----|-----|-------|--------|
| `0xF0001000` | `QSPI_CONFIG` | R/W | 4 byte | QSPI konfiguráció |
| `0xF0001004` | `QSPI_FLASH_ADDR` | R/W | 4 byte | Flash cím (olvasandó pozíció) |
| `0xF0001008` | `QSPI_BINARY_SIZE` | R/O | 4 byte | Binary mérete |
| `0xF000100C` | `QSPI_DATA` | R/O | 1 byte | Olvasott bájt |

## MMIO térkép összefoglaló

```
0xF0000100 ┬─ Core felderítés (6 regiszter)
0xF0000200 ┼─ Mailbox base address
0xF0000300 ┼─ Per-core mailbox enable (N regiszter)
0xF0000400 ┼─ Per-core status (N regiszter)
0xF0000600 ┼─ Interrupt controller (3 vektor)
0xF0000800 ┼─ Mailbox address table (N regiszter)
0xF0001000 ┼─ QSPI Flash controller (4 regiszter)
0xF0002000 ┴─ Seal Core interfész (5 regiszter)
```

## Boot szekvencia MMIO hozzáférési sorrend

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

## Jelenlegi szoftveres megoldás

A .NET referencia implementációban nincs MMIO — a `TActorSystem` közvetlenül kezeli a belső állapotot. A boot szekvencia kódja (`src/Symphact.Boot/`) még nem létezik.

## Nyitott kérdések (HW visszajelzés szükséges)

1. **Cím tartomány** — a `0xF0000000–0xF000FFFF` tartomány elfogadható-e a MMIO régióknak, vagy a HW más elrendezést preferál?
2. **Regiszter szélesség** — mindenhol 32-bit (4 byte), vagy van, ahol 8/16 bit elegendő?
3. **Atomi olvasás** — a `SEAL_CORE_SIGNAL` polling-gal olvasandó, vagy van interrupt is?
4. **Max core szám** — a `core_id×4` offset hány core-ig skálázható? (F6: 16 Nano + 6 Rich = 22 → ~88 byte; 10k core → ~40 KB)
5. **QSPI vs OPI** — az architecture doc OPI-t említ F6-ra; a QSPI regiszterek OPI-re módosítandók?

## Kereszthivatkozások

- Symphact boot sequence: `docs/boot-sequence-hu.md` — 1–8. lépés
- CLI-CPU architecture: `docs/architecture-hu.md` — memória térkép szekció
- OSREQ-001: fa topológia (cluster regiszterek: `0xF0000108–0xF0000114`)


---

## Changelog

| Verzió | Dátum | Összefoglaló |
|--------|-------|-------------|
| 1.0 | 2026-04-20 | Kezdeti kiadás |
