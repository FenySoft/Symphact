# OSREQ-007: Aktor referencia formátum, HMAC verify HW unit, és trust anchor szigorítás

> English version: [osreq-007-actor-ref-format-en.md](osreq-007-actor-ref-format-en.md)

> **Állapot:** Draft — hardveres visszajelzésre vár
>
> **Érintett CFPU fázis:** F6 (silicon, ChipIgnite tape-out), F6.5 (Secure Edition opcionális megerősítés)

## Összefoglaló

A Symphact véglegesítette a `TActorRef` 64 bites bit-elrendezését (`docs/actor-ref-scaling-hu.md` v2.0). A specifikáció **három HW követelményt** támaszt a CLI-CPU csapatnak:

1. **Interconnect cella header v2.5**: a `src_actor`/`dst_actor` mezők 16 → 8 bit, plusz új `perms[8]` és `HMAC[24]` mezők (a header **összmérete 128 bit változatlan** marad)
2. **Új HW komponens**: célcore mailbox-edge HMAC verify unit (per-core kulcs, fail-stop trigger, hibás-HMAC counter)
3. **Trust anchor szigorítás**: az `eFuse` egyetlen `CaRootHash` slotot tartalmaz (NEM array, NINCS Open-mode bit, NINCS deployment-mode toggle)

Ezek **nem opcionálisak** — a Symphact védelmi piramisa a HW oldali implementációra támaszkodik.

## Háttér — miért most

A `TActorRef` a Symphact publikus API alapköve. A v1.0 spec a `roadmap.md` M0.7-ben rögzített `[core-id:16][offset:16][HMAC:24][perms:8]` layout **inkompatibilis** volt a CLI-CPU `interconnect-hu.md` v2.4 header-rel és `architecture-hu.md` 24-bites HW címével. A `actor-ref-scaling-hu.md` v2.0 doksi feloldja az inkonzisztenciákat, és a CLI-CPU oldali kanonikus szélességekkel **bit-azonos** layout-ot rögzít:

```
TActorRef (64 bit, opaque, public):
[HMAC:24][perms:8][actor-id:8][core-coord:24]
   │        │         │              │
   ▼        ▼         ▼              ▼
Header HMAC  Header   Header dst_actor  Header dst
(új mező)    perms    (új mező)         (létezik)
            (új mező)
```

A CLI-CPU header már 128 bit (16 byte), a TActorRef 64 bitje **az alsó 64 bitnek felel meg** — a többi 64 bit (src + src_actor + control mezők) változatlan a v2.4-hez képest, de **az allokáció módosul**.

## Követelmény 1 — Interconnect cella header v2.5

### Jelenlegi v2.4 (`interconnect-hu.md:72-99`)

```
Header (16 byte = 128 bit):
  dst[24] + src[24]                     — routing
  + src_actor[16] + dst_actor[16]       — actor ID
  + seq[8] + flags[8] + len[8] + CRC-8[8]  — control
  + reserved[16]                        — jövőbeli
                                        Σ = 128 bit
```

### Javasolt v2.5

```
Header (16 byte = 128 bit, méret VÁLTOZATLAN):
  dst[24] + src[24]                     — routing
  + src_actor[8] + dst_actor[8]         — actor ID (16→8 bit)
  + perms[8] + HMAC[24]                 — capability + autentikáció (ÚJ)
  + seq[8] + flags[8] + len[8] + CRC-8[8]  — control
                                        Σ = 128 bit
```

| Mező | v2.4 | v2.5 | Változás |
|---|---|---|---|
| dst | 24 bit | 24 bit | — |
| src | 24 bit | 24 bit | — |
| src_actor | 16 bit | **8 bit** | csökkentés |
| dst_actor | 16 bit | **8 bit** | csökkentés |
| perms | (nincs) | **8 bit** | ÚJ |
| HMAC | (nincs) | **24 bit** | ÚJ |
| seq | 8 bit | 8 bit | — |
| flags | 8 bit | 8 bit | — |
| len | 8 bit | 8 bit | — |
| CRC-8 | 8 bit | 8 bit | — |
| reserved | 16 bit | (eltűnt) | felhasználva |

### Az `actor-id` 16 → 8 bit csökkentés indoklása

A v2.4 indoklása ("max 65 536 aktor / core, lefedi az alvó aktorokat is") **felesleges plafont** ad. Egy core SRAM-ban realisztikusan élő aktorok száma:

| Core típus | SRAM | Reális aktor/core |
|---|---|---|
| Nano (4 KB) | 4 KB | 1–10 |
| Actor (64 KB) | 64 KB | 10–100 |
| Rich (256 KB) | 256 KB | 50–500 |

8 bit (256 actor/core) **bőven elég**, és a felszabaduló 16 bit a védelmi piramis kritikus elemeit (perms + HMAC) tárolja.

### Wire format ↔ TActorRef bit-azonosság

A `TActorRef` 64 bitje az `dst[24] + dst_actor[8] + perms[8] + HMAC[24]` szegmensre képződik le 1:1 (Send-kor a runtime-ban nincs konverzió). Ez a leg-elegánsabb wire format-ref kapcsolat.

### DDR5 CAM tábla (`ddr5-architecture-hu.md:113-114`)

Konzisztencia érdekében a CAM tábla `src_actor[16]` mezője is **8 bitre módosul**:

```
| src[24]  | src_actor[8]  | DDR5 Start | DDR5 End | Jog |
```

## Követelmény 2 — Célcore mailbox-edge HMAC verify HW unit

### Cél

Minden bejövő üzenet HMAC autentikációja a célcore mailbox FIFO **bemenetén**, NEM az interconnect router-ben. Ez illeszkedik a CFPU `architecture-hu.md:1326` "egy mailbox IRQ per core" elvéhez.

### HW unit specifikáció

```
Bemenet:
  - Bejövő cella header (128 bit a routerből)
  - Per-core HMAC kulcs (256 bit, sealed Quench-RAM-ban / on-chip SRAM)

Számítás:
  M       = dst[24] || dst_actor[8] || perms[8] || (constant nonce)
  K       = per_core_key
  tag_full = SipHash-128(K, M)            // ~10 cycle @ 500 MHz
  expected_HMAC = tag_full[127:104]        // MSB 24 bit (NIST SP 800-107)

Ellenőrzés:
  if (header.HMAC == expected_HMAC):
      pass → cella mailbox FIFO-ba
  else:
      drop + fail-stop trigger + counter increment
```

### Költségbecslés

| Tétel | Érték |
|---|---|
| HW area / verify unit | ~5 000 gate (SipHash-128 ~3-4k + compare/control ~1-2k) |
| Verify ciklus | ~10 cycle @ 500 MHz = **20 ns** |
| Per-chip költség (88k Nano core) | ~440M gate ≈ **2,2 mm² 5nm-en** (~0,15% a 1494 mm² package-ből) |
| Throughput | 5 × 10⁷ verify/sec/core (sequential) |

### Algoritmus választás indoklása — SipHash-128

| Algoritmus | Area | Verify cycle | Megfelelőség |
|---|---|---|---|
| HMAC-SHA256 trunc | ~30k gate | ~80 cycle | Általános crypto, túlméretezett rövid MAC-ra |
| HMAC-SHA3-256 trunc | ~50k gate | ~100 cycle | PQC-ready, túl drága |
| BLAKE3 | ~15k gate | ~30 cycle | Modern, gyors, de nem MAC-specifikus |
| **SipHash-128** | **~5k gate** | **~10 cycle** | Kifejezetten rövid üzenet MAC-ra tervezett (Aumasson & Bernstein) |
| HMAC-MD5 trunc | ~10k gate | ~40 cycle | Cryptographically broken |

**SipHash-128** a választás: 6× kisebb és 8× gyorsabb, mint az HMAC-SHA256, és kriptografikailag erős rövid üzenet MAC-ra. NIST SP 800-107 szerint a 24 bit MSB-truncate elfogadható (1:16,8M forgery resistance), és a Symphact védelmi piramissal együtt (lásd `actor-ref-scaling-hu.md` "Védelmi piramis") post-quantum-szintű biztonságot ad.

## Követelmény 3 — HW fail-stop és hibás-HMAC counter

### HW fail-stop

Hibás HMAC esetén:

```
1. Cella drop (NEM kerül mailbox FIFO-ba)
2. Supervisor IRQ trigger a KÜLDŐ core felé (a cella header.src mezőjéből kiolvasva)
3. Küldő core CORE_STATUS = Error (osreq-005 IRQ_MAILBOX_FAIL)
4. AuthCode quarantine trigger (lásd lent)
```

Ez a "1 hibás HMAC = küldő core terminálódik" minta. A `osreq-005` jelenleg csak `CORE_STATUS = Error`-t ad, **bővítendő** a küldő-oldali fail-stop trigger-rel.

### Per-chip hibás-HMAC counter

```
HW register: BAD_HMAC_COUNTER (32 bit, monotonikus, NEM nullázható szoftverből)
Threshold:   16 (default, sealed regiszterben konfigurálható BIST-kor)

Increment-elési mód:
  bad_hmac_event → BAD_HMAC_COUNTER++
  if BAD_HMAC_COUNTER >= threshold:
      → IRQ a capability_registry-nek
      → chip-szintű kulcsrotáció
      → BAD_HMAC_COUNTER nullázás (csak ROTATE-tel)
      → supervisor riasztás (audit log)
```

Ez **defense-in-depth réteg**: ha az aláírói blacklist és HW fail-stop ellenére a támadó valahogy próbálkozni tud, a 16. próba után chip-szintű kulcsrotáció történik, ami **érvényteleníti az összes addigi próbálkozási állapotot**.

## Követelmény 4 — AuthCode quarantine integráció

A `authcode-hu.md:135` revocation_list bővítendő a **hibás HMAC esetén automatikus quarantine trigger**-rel:

```
Hibás HMAC esemény:
1. A küldő bytecode SHA-256 hash-e azonosítható a header.src core-ról futó instance-ból
2. Ezt a SHA-t azonnal blacklist-be tesszük (új mechanizmus: BAD_HMAC_BLACKLIST)
3. Az aláíró kulcs cert.SubjectId-jét revocation_list-be helyezzük
4. Az aláíró ÖSSZES többi futó bytecode-jét supervisor terminate-eli
5. Az aláíró összes EDDIGI bytecode-ját blacklist-be tesszük (preventive)
```

**Indoklás**: ha egy aláíró bármelyik bytecode-ja rosszindulatú HMAC-et generál, az aláíró **bizonyítottan kompromittált** vagy maga a támadó. Az "issuer-trust quarantine" minta — egyetlen rossz tett = az aláíró megbízhatatlan.

## Követelmény 5 — Trust anchor szigorítás

### Az `eFuse.CaRootHash` mező

A `authcode-hu.md:90` szerint a Seal Core a `BitIce.Verify(cert, eFuse.CaRootHash)` lépéssel ellenőrzi a cert-chain-t. Ez a **trust anchor** — minden cert vissza kell vezessen ehhez a hash-hez.

### Szigorú konfiguráció

```
eFuse {
  CaRootHash : 256 bit                  ← OTP, EGYETLEN slot
                                        ← FenySoft master key SHA-256-tal programozva
                                        ← gyártás után fizikailag NEM módosítható
}

Tilos:
  - Multi-root array (CaRootHash[0..N])
  - Open-mode bit / developer-chip bit
  - Deployment-mode toggle
  - Bármilyen runtime override mechanizmus a trust decision-re
```

### Indoklás

**Multi-root array** — gyártás utáni támadási vektor: ha a chip több root hash-t fogad el, egy spare slot támadó saját kulccsal beégethető (akár garanciaidőtartam alatt). Az **egyetlen OTP slot** fizikailag korlátoz: ha programozva van, soha nem cserélhető.

**Open-mode bit** — bármilyen "developer chip" megkülönböztető bit **támadási felület**. A támadó beállíthatja és máris back-door-ral rendelkezik. A **NEM létezik konfiguráció** = a támadó nem találhat shortcut-ot.

**Deployment-mode toggle** — runtime trust decision-t adna, ami szembemegy a `authcode-hu.md` SHA-256 binding kötelező invariánsával. Eltávolítandó minden ilyen mechanizmus.

A FenySoft termékvonal **kötelezően** ezt a szigorú konfigurációt használja (lásd `docs/trust-model-hu.md`). Ez a Symphact védelmi piramisának alapfeltétele.

## Hatás-becslés

| Követelmény | HW erőfeszítés | Tape-out kockázat |
|---|---|---|
| Header v2.5 mezőszélességek | RTL átrendezés a cella header SRAM-ban (~1 hét) | Alacsony — méret változatlan, csak az allokáció módosul |
| Mailbox-edge HMAC verify unit | Új HW blokk (SipHash-128 + compare/control), per-core (~2 hét) | Közepes — új komponens, FPGA-n verifikálandó (F4 fázisban) |
| HW fail-stop bővítés | Az `IRQ_MAILBOX_FAIL` (osreq-005) kibővítése küldő-oldali IRQ-val (~3 nap) | Alacsony |
| Per-chip BAD_HMAC_COUNTER | Új HW register + threshold compare + IRQ kapcsolat (~3 nap) | Alacsony |
| Trust anchor szigorítás | OTP eFuse single-slot konfiguráció (a meglévő F6 design-ban már egy slot) | Alacsony — már megfelel, csak ne kerüljön array-szerű extension |

**Becsült összes erőfeszítés**: ~3-4 mérnökhét a CFPU csapatra a F6-Silicon One tape-out előtti revízióhoz.

## Nyitott kérdések

1. **HMAC counter threshold értéke** — javaslat: 16. CFPU csapat egyetértésével.
2. **SipHash-128 vs alternatívák** — érdemes-e BLAKE3 / Poly1305 / CMAC-AES variánst is mérlegelni? A SipHash-128 a default, de a CFPU csapat áttekintheti.
3. **Per-core kulcs származtatás** — a master kulcsból KDF-fel (HKDF), vagy minden core-nak külön random kulcs a gyártáskor? KDF egyszerűbb (egy master kulcs sealed), de a `capability_registry`-nek a master-t kell tartania.
4. **Inter-chip HMAC algoritmus** (osreq-006) — ugyanaz SipHash, vagy erősebb (HMAC-SHA256 / Poly1305)? A chip-en kívüli kommunikáció szigorúbb threat modellel.
5. **BAD_HMAC_BLACKLIST tárhely-méret** — kilobyte vagy megabyte? Storage saturation védelem mértékét határozza meg.

## Kapcsolódó tervek és milestone-ok

- **`Symphact/docs/actor-ref-scaling-hu.md`** — TActorRef bit-elrendezés és védelmi piramis
- **`Symphact/docs/trust-model-hu.md`** — FenySoft Strict whitelist üzleti modell
- **`Symphact/docs/roadmap-hu.md`** M0.6, M0.7, M2.5 — implementációs milestone-ok
- **`Symphact/docs/vision-hu.md`** — capability-based security és location transparency
- **`CLI-CPU/docs/architecture-hu.md`** — 24-bites HW cím, Actor Scheduling Pipeline
- **`CLI-CPU/docs/interconnect-hu.md`** — cella header v2.4 (változtatandó v2.5-re)
- **`CLI-CPU/docs/ddr5-architecture-hu.md`** — CAM tábla v2.4 (`src_actor` 16→8)
- **`CLI-CPU/docs/authcode-hu.md`** — AuthCode trust chain, revocation_list
- **`CLI-CPU/docs/quench-ram-hu.md`** — sealed kulcs storage
- **`CLI-CPU/docs/security-hu.md`** — eliminált CWE-k

## Verziótörténet

| Verzió | Dátum | Változás |
|---|---|---|
| 1.0 | 2026-04-25 | Első verzió: header v2.5, mailbox-edge HMAC verify unit, fail-stop, counter, AuthCode quarantine integráció, trust anchor szigorítás |
