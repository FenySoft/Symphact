# OSREQ-003: Core reset mechanizmus — supervisor restart támogatás

> English version: [osreq-003-core-reset-en.md](osreq-003-core-reset-en.md)

> **Állapot:** Draft — hardveres visszajelzésre vár
>
> **Érintett CFPU fázis:** F4 (multi-core FPGA), F5 (heterogén), F6 (silicon)

## Összefoglaló

A Symphact **„let it crash"** modellje megköveteli, hogy egy supervisor actor újra tudja indítani egy hibás child actor core-ját. Ehhez **hardveres core reset** szükséges, ami atomilag törli a core SRAM-ját és mailbox FIFO-ját, majd a core-t újraindítható állapotba hozza.

## Kontextus

Az Erlang/OTP supervision modell a Symphact alapja (M0.3 milestone). Amikor egy actor hibát dob:

```
1. Actor crash (pl. hibás üzenet, assertion failure)
2. Trap → supervisor értesítés (parent actor)
3. Supervisor dönt: Restart / Stop / Escalate
4. Restart esetén: core reset → új actor state → folytatás
```

A **restart** a leggyakoribb stratégia. CFPU-n ez azt jelenti: a Nano/Rich core **hardveresen** visszaáll egy tiszta állapotba.

## Javasolt hardveres viselkedés

### Core Reset regiszter

| Cím | Név | R/W | Méret | Leírás |
|-----|-----|-----|-------|--------|
| `0xF0000500 + core_id×4` | `CORE_RESET[n]` | W/O | 4 byte | `1` írása triggereli a core reset-et |

### Reset szekvencia (HW)

```
1. CORE_RESET[n] = 1 írás (Rich Core / supervisor által)
2. HW: target core execution HALT (azonnali, nem vár üzenet-határ)
3. HW: target core SRAM → zero-fill (teljes, beleértve stack + heap + locals)
4. HW: target core mailbox FIFO → flush (minden pending üzenet eldobva)
5. HW: target core PC = 0 (vagy boot entry point)
6. HW: CORE_STATUS[n] = 3 (Reset)
7. HW: target core HALT állapotban marad — a scheduler/supervisor dönt az újraindításról
```

### Újraindítás (SW)

```
8. SW: Scheduler új CIL kódot tölt a core SRAM-ba (flash-ről vagy Rich Core-ból)
9. SW: Mailbox ENABLE[n] = 1
10. SW: Core wake signal (→ CORE_STATUS[n] = 1, Running)
```

## Miért kell HW support?

| Megoldás | Probléma |
|----------|---------|
| **SW-only reset** (Rich Core írja felül a Nano SRAM-ot) | A Rich Core **nem látja** a Nano core SRAM-ját (shared-nothing!) |
| **Self-reset** (a Nano core saját magát törli) | Ha a core crash-elt, nem bízhatunk meg benne |
| **HW reset vonal** | ✅ Megbízható, atomi, a core-tól független |

## Biztonsági megfontolások

- **Csak a supervisor core** (Rich Core) írhatja a `CORE_RESET[n]` regisztert — capability check szükséges
- Egy Nano core **nem resetelheti** saját magát vagy más Nano core-ot — csak a Rich Core (supervisor)
- A reset **nem törölheti** a Seal Core-t — az hardwired, immutable
- **Mailbox flush** fontos: ne maradjanak mérgezett üzenetek a FIFO-ban

## Mért hatás (becsült)

| Művelet | SW-only (ha lehetséges lenne) | HW reset |
|---------|-------------------------------|----------|
| SRAM clear (4 KB Nano) | ~1000 ciklus (byte-onként) | ~10-50 ciklus (HW zero-fill) |
| Mailbox flush | ~64 ciklus (slot-onként) | ~1-2 ciklus (pointer reset) |
| Teljes restart | ~2000 ciklus | ~100 ciklus |

A **let-it-crash** modellben a restart **gyakori, normális művelet** — nem kivétel. Ezért a latencia számít.

## Nyitott kérdések (HW visszajelzés szükséges)

1. **Partial reset** — kell-e SRAM partial clear (pl. csak heap, stack megmarad)? Vagy mindig full wipe?
2. **Mailbox drain vs flush** — a pending üzenetek eldobandók (flush), vagy kiolvashatók a supervisor által (drain)?
3. **Reset idő garancia** — determinisztikus-e a reset idő? (fontos real-time use case-ekhez)
4. **Cascade reset** — ha egy cluster supervisor resetelődik, az egész cluster resetelődik? Vagy core-onként?
5. **Reset ok regiszter** — a CORE_STATUS mellett kell-e `CORE_RESET_REASON` regiszter (trap code, watchdog, explicit reset)?

## Kereszthivatkozások

- Symphact roadmap: M0.3 (Supervision / Let-it-crash)
- Boot sequence: `docs/boot-sequence-hu.md` — 1. lépés (POR)
- CLI-CPU architecture: Seal Core self-test analógia
