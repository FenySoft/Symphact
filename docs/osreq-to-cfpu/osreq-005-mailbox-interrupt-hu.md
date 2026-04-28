# OSREQ-005: Mailbox interrupt vs polling — core értesítési mechanizmus

> English version: [osreq-005-mailbox-interrupt-en.md](osreq-005-mailbox-interrupt-en.md)

> Version: 1.0

> **Állapot:** Draft — hardveres visszajelzésre vár
>
> **Érintett CFPU fázis:** F4 (multi-core FPGA), F5 (heterogén), F6 (silicon)

## Összefoglaló

A Symphact scheduler-nek (M0.4) tudnia kell, hogyan értesül egy core arról, hogy **új üzenet érkezett a mailbox FIFO-jába**. Ez alapvető kérdés: **hardware interrupt** vagy **software polling**? A válasz befolyásolja az energiafogyasztást, a latenciát és a scheduler algoritmust.

## Kontextus

A CFPU event-driven modellje szerint a core **alszik**, amíg üzenet nem érkezik. De ennek a mechanikája nincs specifikálva:

```
Üzenet érkezik N5 mailbox FIFO-jába
  → ??? → N5 core felébred és feldolgozza
```

A `???` a kérdés.

## Opciók

### A) Hardware interrupt (javasolt)

```
1. Üzenet → N5 inbox FIFO (HW write)
2. HW: inbox not-empty → IRQ vonal aktív
3. HW: core wake (ha sleeping) vagy IRQ handler hívás (ha running)
4. SW: IRQ handler → TryReceive() → actor handler
```

| Előny | Hátrány |
|-------|---------|
| Zero-latency wake (1-2 ciklus) | IRQ vonal szükséges core-onként |
| Minimális energiafogyasztás (core valóban alszik) | IRQ controller terület-költség |
| Determinisztikus latencia | Edge case: IRQ storm (sok üzenet egyszerre) |

### B) Software polling

```
1. Üzenet → N5 inbox FIFO (HW write)
2. SW: core periodikusan poll-olja az inbox status regisztert
3. SW: if not-empty → TryReceive() → actor handler
```

| Előny | Hátrány |
|-------|---------|
| Egyszerűbb HW (nincs IRQ vonal) | Core nem alhat → folyamatos energiafogyasztás |
| Nincs IRQ overhead | Polling latencia (worst case = poll intervallum) |
| | CPU ciklus-pazarlás üres poll-okra |

### C) Hybrid (interrupt + coalescing)

```
1. Üzenet → inbox FIFO
2. HW: N üzenet összegyűlik VAGY T ciklus eltelik → egyetlen IRQ
3. SW: batch receive → actor handler
```

| Előny | Hátrány |
|-------|---------|
| Csökkenti az IRQ storm-ot | Bonyolultabb HW (counter + timer) |
| Jobb throughput nagy terhelésnél | Növeli a worst-case latenciát |

## Javasolt megoldás

**A) Hardware interrupt** a Nano core-okhoz, **C) Hybrid** a Rich Core-hoz:

- **Nano core-ok** egyszerűek — egy actor, egy mailbox, ritkább üzenet → tiszta interrupt a leghatékonyabb
- **Rich Core** sok kernel actor-t futtat time-sliced → interrupt coalescing csökkenti az overhead-et

### Javasolt regiszterek

| Cím | Név | R/W | Leírás |
|-----|-----|-----|--------|
| `0xF0000600` | `IRQ_MAILBOX_HANDLER` | R/W | Mailbox not-empty IRQ handler cím |
| `0xF0000610 + core_id×4` | `MAILBOX_IRQ_ENABLE[n]` | R/W | Per-core mailbox IRQ enable |
| `0xF0000620` | `IRQ_COALESCE_COUNT` | R/W | Rich Core: hány üzenet után IRQ (0 = minden üzenetnél) |
| `0xF0000624` | `IRQ_COALESCE_TIMEOUT` | R/W | Rich Core: max várakozás ciklusokban (fallback timer) |

### Wake mechanizmus

| Core állapot | Mailbox üzenet érkezik | Viselkedés |
|-------------|----------------------|------------|
| **Sleeping** | inbox IRQ → **wake** | Core felébred, PC = IRQ handler |
| **Running** | inbox IRQ → **pending** | IRQ handler hívódik a jelenlegi utasítás-határ után |
| **Reset** | inbox IRQ → **eldobva** | Core nem ébredt fel, üzenet a FIFO-ban marad |

## Energiafogyasztási hatás

| Modell | 10k Nano core, átlag 1% aktív | Fogyasztás |
|--------|-------------------------------|------------|
| Polling (mind fut) | 10k × ~100 µW | ~1 W |
| Interrupt (99% alszik) | 100 × 100 µW + 9900 × ~1 µW | ~20 mW |

**50× különbség** — ez a CFPU egyik kulcs értékajánlata (ultra-low power).

## Nyitott kérdések (HW visszajelzés szükséges)

1. **IRQ vonal topológia** — per-core dedikált IRQ vonal, vagy cluster-szintű multiplexált?
2. **Nested IRQ** — ha mailbox IRQ handler fut és újabb üzenet érkezik, nested IRQ vagy pending?
3. **Wake latencia** — hány ciklus a sleep → running átmenet? (target: ≤5 ciklus)
4. **IRQ prioritás** — mailbox vs watchdog vs trap — fix prioritás vagy programozható?
5. **Power domain** — a sleeping core clock-gated, power-gated, vagy mindkettő?

## Kereszthivatkozások

- Symphact roadmap: M0.4 (Scheduler / Per-Actor Parallelism)
- CLI-CPU architecture: „Event-driven, nem clock-driven" szekció, Sleep/Wake logika
- OSREQ-002: MMIO térkép (IRQ regiszterek)


---

## Changelog

| Verzió | Dátum | Összefoglaló |
|--------|-------|-------------|
| 1.0 | 2026-04-20 | Kezdeti kiadás |
