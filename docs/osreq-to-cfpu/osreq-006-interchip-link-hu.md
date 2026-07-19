# OSREQ-006: Inter-chip link protokoll — elosztott fabric kommunikáció

> English version: [osreq-006-interchip-link-en.md](osreq-006-interchip-link-en.md)

> Version: 1.1

> **Állapot:** Draft — hardveres visszajelzésre vár
>
> **Érintett CFPU fázis:** F6 (silicon), F7 (production)

## Összefoglaló

A Symphact distribution modellje (M0.6 — Remoting) megköveteli, hogy **több CFPU chip** egyetlen logikai fabric-ként működjön. Ehhez szabványosított, alacsony-latenciájú **inter-chip link** kell, amelyen az actor mailbox üzenetek áthaladnak — a location transparency megőrzésével.

## Kontextus

A Symphact alapelve: **`TActorRef` nem árulja el, hogy a cél actor lokális, más core-on, vagy más chip-en van**. Ez location transparency — ugyanaz a `Send(ref, msg)` hívás, akárhol van a cél. De ehhez a HW-nek transzparensen kell routolnia a chip-határokon át.

Az OSREQ-001 fa topológia multi-chip szintre terjed:

```
              Fabric
             /   |   \
         Chip 0  Chip 1  Chip 2
           │       │       │
        Rich     Rich    Rich
        / | \   / | \   / | \
       Cl  Cl  Cl  Cl  Cl  Cl
```

A chip-ek közötti link a **fa gyökere felett** van — ez a leglassabb, de legritkább útvonal.

## Javasolt hardveres viselkedés

### Link fizikai réteg opciók

| Opció | Sebesség | Pin szám | Távolság | Megjegyzés |
|-------|----------|----------|----------|-----------|
| **SPI** | ~10-50 Mbps | 4 (MOSI/MISO/CLK/CS) | ~10 cm | Egyszerű, de lassú |
| **LVDS** | ~100-800 Mbps | 4 (2 differenciál pár) | ~1 m | Gyors, alacsony EMI |
| **UART** | ~1-10 Mbps | 2 (TX/RX) | ~1 m | Nagyon egyszerű, nagyon lassú |
| **Custom mailbox bridge** | ~50-200 Mbps | 4-8 | ~10 cm | A CLI-CPU architecture doc ezt említi |

Az architecture doc `Mailbox bridge (inter-chip): 4 pin`-t említ — ez valószínűleg SPI-szerű vagy custom protokoll.

### Üzenet formátum

Az inter-chip üzenetnek tartalmaznia kell:

```
┌─────────────────────────────────────────────────────┐
│ Header (fix méret)                                   │
├──────────┬──────────┬──────────┬──────────┬─────────┤
│ src_chip │ dst_chip │ dst_cluster │ dst_core │ msg_len │
│ 8 bit    │ 8 bit    │ 8 bit       │ 16 bit   │ 16 bit  │
├──────────┴──────────┴──────────┴──────────┴─────────┤
│ Payload (változó méret, max ??? byte)                │
├─────────────────────────────────────────────────────┤
│ CRC-16 (opcionális, link integritás)                 │
└─────────────────────────────────────────────────────┘
```

### Routing döntés (OSREQ-001-gyel összefüggésben)

A `TActorRef` chip-local marad (`actor-ref-scaling-hu.md` §1.c/§3.c) — nem hordoz chip-id mezőt. Chip-határon túli célzás **proxy-aktor mintával** történik: a küldő egy lokális `remote_proxy` aktorra küld (rendes, chip-local CST index), a proxy belső state-je tartalmazza a tényleges cél chip/cluster/core adatokat.

```
Send(ref, msg):                                ← ref: lokális, chip-local CST index
  ha ref alkalmazás-aktorra mutat        → intra-chip routing (OSREQ-001 fa)
  ha ref egy remote_proxy aktorra mutat  → proxy aktor:
    1. Üzenet serialize (SRAM → link buffer)
    2. Header hozzáadás a proxy belső state-jéből (src/dst chip+cluster+core)
    3. Link transmit (HW)
    4. Célchip: link receive → deserialize → dst mailbox FIFO
```

### Flow control

| Probléma | Megoldás |
|----------|---------|
| Link lassabb, mint a mailbox | **Backpressure**: a link buffer teli → Send blokkolódik vagy NAK |
| Chip offline (tápelvesztés) | **Timeout + supervisor értesítés** → actor migration |
| Link hiba (CRC mismatch) | **Retry** (max 3×) → ha nem sikerül, link down → supervisor |

## Hatás a Symphact-re

| OS komponens | Változás |
|-------------|---------|
| `TActorRef` | ~~`chip-id` mező hozzáadása~~ — **ELVETETT**: az `actor-ref-scaling-hu.md` §3.b döntése szerint a `TActorRef` chip-local marad (32-bit opaque CST index); chip-határon túli címzés proxy-aktor mintával történik (§3.c) |
| `TRouter` | Inter-chip routing: chip-id lookup → link buffer |
| `TSerializer` | Üzenet serialize/deserialize inter-chip átvitelhez |
| `TDistributionSupervisor` | Link health monitoring, actor migration |

## Nyitott kérdések (HW visszajelzés szükséges)

1. **Link típus** — SPI, LVDS, custom? Pin budget?
2. **Max message méret** — fix (pl. 64 byte) vagy változó? Ha fix → nagy üzenetek fragmentálása?
3. **Multi-chip topológia** — pont-pont (daisy chain), csillag (hub), vagy fa (OSREQ-001 kiterjesztés)?
4. **Link sebesség** — target Mbps? Latencia budget (target: <10 µs egy üzenethez)?
5. **Hot plug** — kell-e runtime chip hozzáadás/eltávolítás support?
6. **Encryption** — inter-chip üzenetek titkosítottak? (fizikai hozzáférés elleni védelem)

## Kereszthivatkozások

- Symphact roadmap: M0.6 (Remoting / Distribution)
- CLI-CPU architecture: `Mailbox bridge (inter-chip): 4 pin`, pin budget tábla
- OSREQ-001: fa topológia (multi-chip kiterjesztés)
- OSREQ-002: MMIO térkép (`CHIP_ID` regiszter)


---

## Changelog

| Verzió | Dátum | Összefoglaló |
|--------|-------|-------------|
| 1.1 | 2026-07-19 | „Hatás a Symphact-re" tábla: `TActorRef` chip-id mező javaslat jelölve ELVETETT-ként (`actor-ref-scaling-hu.md` §3.b); routing döntés pseudocode proxy-aktor mintára javítva |
| 1.0 | 2026-04-20 | Kezdeti kiadás |
