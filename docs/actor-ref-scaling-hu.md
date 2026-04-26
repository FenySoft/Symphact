# Aktor referencia skálázódás és védelmi modell — Symphact

> English version: [actor-ref-scaling-en.md](actor-ref-scaling-en.md)

> Version: 2.0

> Státusz: véglegesített specifikáció, M0.6 / M0.7 / M2.5 alapja

Ez a dokumentum a `TActorRef` **véglegesített bit-elrendezését**, **wire-formátumát**, és a **védelmi piramist** rögzíti. Az érvelés végigmegy a tervezési döntéseken: miért 64 bit, miért ez az allokáció, miért 24 bit HMAC, és miért elegendő a védelem a Symphact célközönségére (consumer + enterprise + ipari kritikus).

> **Célközönség:** Symphact runtime fejlesztők, CFPU HW tervezők (sister repo: `FenySoft/CLI-CPU`), API kontraktus felülvizsgálatakor, biztonsági auditok.

---

## Kontextus

A `TActorRef` a Symphact publikus API-jának **alapköve**: minden aktor-aktor kommunikáció ezen keresztül történik. A `CLAUDE.md` rögzíti az API kontraktust:

```csharp
public readonly record struct TActorRef(long ActorId);   // 64 bit, opaque, public
```

A 64 bit **chip-local** jelentésű, és **bit-azonos** a CLI-CPU interconnect cella header alsó szegmensével (lásd alább a wire-formátum szekciót). A felhasználó a runtime-belüli reprezentáció **belsejébe** nem nyúl — a `long` opaque token, csak `Equals`/`GetHashCode`/Send-paraméter szempontból érdekes.

A korábbi spec-eltérések (vision-hu.md 160 bites struct, roadmap M0.7 `[16][16][24][8]`) **inkonzisztenciát képeztek** a CLI-CPU oldali 24-bites HW címmel és 16-bites `src_actor`/`dst_actor` mezőkkel. Ez a v2.0 doksi feloldja az inkonzisztenciákat.

---

## Threat model

A Symphact védelmi modellje **szoftveres + supply-chain** támadásokra fókuszál. A fizikai szintű támadás **out-of-scope** ezen a védelmi rétegen.

| Támadási vektor | Védelem | Hatókör |
|---|---|---|
| Szoftveres brute-force (chip-belül vagy hálózat felől) | HMAC verify + HW fail-stop + counter | **In-scope** |
| Másik aktor olvassa egy aktor kulcsát | Shared-nothing per-core SRAM isolation | **In-scope** |
| Kompromittált aláíró (rosszindulatú szoftver) | AuthCode + supply-chain quarantine | **In-scope** |
| Cold boot (kikapcsolás után fagyasztás) | Quench-RAM RELEASE atomi wipe (mellékhatás) | **In-scope** |
| **Bus-MITM, chip-decap, FIB-szondázás** | — | **Out-of-scope** (fizikai hozzáférés = SSD ellopás egyszerűbb) |
| **Side-channel (timing, power)** | — | **Out-of-scope** alapszinten (Secure Edition F6.5-re tartozik) |

Ez a Linux/Windows/macOS szintű commercial threat model, és csak a Secure Element (F6.5) megy tovább a fizikai védelemre. Production deployment-eknél ez **explicit dokumentumban rögzítendő** (lásd [`trust-model-hu.md`](trust-model-hu.md)).

---

## A skálázódási kihívás

Egy CFPU chip core-szám referencia (`CLI-CPU/docs/core-types-hu.md`, 18 tine die @ 5nm, 1 494 mm²):

| Core típus | Node méret | Core-szám / package | Bit szükséglet |
|---|---|---|---|
| Nano | 0,017 mm² | **~87 900** | 17 bit |
| Actor | 0,032 mm² | ~46 700 | 16 bit |
| Rich | 0,071 mm² | ~21 000 | 15 bit |

Skálázódási projekció (`CLI-CPU/docs/chiplet-packaging-hu.md`):
- 2030 (2nm, 3D SRAM, 8 chiplet): 131 072 core/package → 17 bit
- 2033+ (1,4nm): 262 144 core/package → 18 bit
- F-9+ extrapoláció (1–10M): 20–24 bit

A **24 bites core-coord** (16M core) elegendő F-9 generációkig — a CLI-CPU `architecture-hu.md:1320` 24-bites HW címével pontosan egyezik.

---

## 1. döntés: A `TActorRef` 64 bit marad mint API kontraktus

### 1.a) Elvetett: 160 bites struct (`vision-en.md` korábbi változata)

Egy 4 mezős struct (CoreId int + MailboxIndex int + CapabilityTag long + Permissions int) komplexitást ad a felhasználóhoz, és a HW interconnect fejléc-mérete **nő**.

**Miért elvetettük:**
- A capability **opaque token** kell legyen — a fejlesztő ne tudjon a tartalmáról
- 160 bit verbatim átvitele a tree fabric-on ~5–10% area növekedést ad
- A komplexitás **a használat oldalán** jelenik meg, nem a runtime-on (ahova való)

### 1.b) Elvetett: 128 bit (`UInt128`) most

A 128 bit minden bit-mérleg problémát megoldana, **de**:
- A CLI-CPU interconnect cella header **128 bit** = 16 byte. Ebből 64 bit **már most lefedi** a TActorRef összes mezőjét (lásd 2. döntés). 128 bites ref felesleges duplázást okozna.
- Pre-alpha (0.1) verzió, az API-törés ára most a legalacsonyabb, **mégsem szükséges**

### 1.c) Végső döntés: 64 bit, opaque, chip-local jelentéssel

A `TActorRef` változatlanul `readonly record struct(long ActorId)`. A 64 bit chip-local — chip-en kívüli címzés proxy-aktor mintával (lásd 3. döntés).

---

## 2. döntés: A 64 bit allokációja

A CLI-CPU interconnect header és DDR5 CAM tábla már rögzíti a kanonikus mezőszélességeket. A `TActorRef` ezekkel **bit-azonos** kell legyen, hogy a Send-kor nincs konverzió.

### 2.a) Roadmap eredeti `[core-id:16][offset:16][HMAC:24][perms:8]`

Ez **inkompatibilis** a CLI-CPU oldallal:
- `core-id:16` < CLI-CPU `dst[24]` (24 bit HW cím)
- `offset:16` (mailbox offset) ≠ CLI-CPU `dst_actor[16]` (actor azonosító)

Ez a roadmap értékek **félreértésen alapultak** — a "mailbox offset" valójában az actor-id, és a HW cím 24 bites a CFPU-ban.

### 2.b) Y3 javaslat: `[HMAC:64][reserved:16][perms:8][actor-id:16][core-coord:24]` (128 bit)

CLI-CPU-kompatibilis 128 biten, de **felesleges**: a header már 128 bit, és a TActorRef 64 biten elfér.

### 2.c) Végső döntés: `[HMAC:24][perms:8][actor-id:8][core-coord:24]` (64 bit)

```
 63          40 39    32 31    24 23                            0
┌──────────────┬────────┬────────┬─────────────────────────────────┐
│  HMAC tag    │ perms  │actor-id│       core-coord                │
│   24 bit     │  8 bit │  8 bit │        24 bit                   │
└──────────────┴────────┴────────┴─────────────────────────────────┘
```

| Mező | Szélesség | Indoklás |
|---|---|---|
| `core-coord` | 24 bit | = CLI-CPU `dst[24]` az interconnect headerben. 16M core kapacitás → F-9+ generációkra elegendő |
| `actor-id` | 8 bit | 256 actor/core. CLI-CPU `dst_actor` 16-ról 8-ra csökken (lásd osreq-007) — egy core-on tipikusan 1–100 aktor él (SRAM-korlát) |
| `perms` | 8 bit, **bit-flag** | 8 capability flag: Send, Stop, Watch, Delegate, Revoke, Query, Snapshot, Migrate |
| `HMAC` | 24 bit | SipHash-128 MSB-truncate. A védelem több rétegű (lásd 5. döntés és brute-force költségelemzés) |

### 2.d) `actor-id` csökkentés indoklása

A CLI-CPU `interconnect-hu.md:75, 93` jelenleg `src_actor[16]` és `dst_actor[16]`-et ír elő ("max 65 536 aktor / core"). Ez **felesleges plafon**:
- Nano core (4 KB SRAM): tipikusan 1–10 aktor
- Actor core (64 KB SRAM): tipikusan 10–100 aktor
- Rich core (256 KB SRAM): tipikusan 50–500 aktor

8 bit (256 actor/core) bőven elegendő, a felszabaduló 16 bit a header `perms[8]` és `HMAC[24]` mezőire kerül (lásd osreq-007 header v2.5 javaslat).

---

## 3. döntés: Chip-en kívüli címzés (inter-chip kommunikáció)

A 24 bites core-coord **egy chip belső címtere**. Multi-chip fabric esetén proxy-aktor minta:

### 3.a) Elvetett: flat globális core-id

20+ bites flat core-id a HMAC-et a kriptografikus szint alá szorítja. A flat címtér **nem létező absztrakció** — a fizikai topológia hierarchikus.

### 3.b) Elvetett: hierarchikus cím a ref-ben

`[chip-id:8][core-id:16][...]` típusú felosztás csak elhalasztja a problémát, és a felhasználó kezdi értelmezni a ref-et (NEM opaque).

### 3.c) Végső: proxy-aktor minta

A `vision-hu.md` location transparency:

> Az aktorok futás közben áthelyezhetők core-ok között — ugyanaz a `Send(actor_ref, msg)` működik minden esetben, és a router (hardveres + szoftveres) eldönti, hová kerül az üzenet.

A `TActorRef` mindig **chip-local**. Egy chipen kívüli aktorhoz a kommunikáció **proxy-aktoron keresztül** megy:

```
Alkalmazás aktor (chip A, core 17)
        │
        │  Send(remoteProxyRef, msg)    ← lokális 64 bit ref
        ▼
[remote_proxy aktor] (chip A, core 0)   ← belső state-ben:
        │                                  { TargetChipId, TargetCoreId,
        │                                    InterChipHmac, … }
        │  inter-chip link write (osreq-006)
        ▼
   Másik chip → célaktor
```

**Erősségek:**
- A 64 bit ref örökre elég (chip-local jelentés)
- Két HMAC, két kulcskör (chip-belüli + inter-chip), defense-in-depth
- Location transparency: az alkalmazás aktor csak ref-eket lát
- Bevált minta: Akka.Remote, Erlang/OTP distributed, Pony ORCA

**Implementációs csomópontok:**
- M0.6 Remoting milestone: első proxy minta szoftveres TCP transport-tal
- M0.7 CFPU HW Integration: a proxy `inter-chip link` MMIO-ra (osreq-006) áll át
- A proxy aktor maga is supervised (M0.3) — crash esetén újraindul

---

## 4. döntés: Wire-formátum — a CLI-CPU interconnect headerrel egyezés

A CLI-CPU `interconnect-hu.md` v2.5 (osreq-007 javasolt módosítás) header-struktúra:

```
┌───────────┬───────────┬─────────────────────────────────────────────────────────┐
│   Mező    │ Szélesség │                       Jelentés                          │
├───────────┼───────────┼─────────────────────────────────────────────────────────┤
│ dst       │ 24 bit    │ Cél core HW cím                                         │
│ src       │ 24 bit    │ Forrás core HW cím (HW által kitöltött, nem hamisítható)│
│ src_actor │ 8 bit     │ Küldő aktor ID — max 256 aktor / core                   │
│ dst_actor │ 8 bit     │ Cél aktor ID                                            │
│ perms     │ 8 bit     │ Aktor capability bit-flag                               │
│ HMAC      │ 24 bit    │ SipHash-128 MSB-truncate                                │
│ seq       │ 8 bit     │ Sorszám (fragmentált üzenetek sorrendje)                │
│ flags     │ 8 bit     │ VN0/VN1, relay flag                                     │
│ len       │ 8 bit     │ Payload byte-szám                                       │
│ CRC-8     │ 8 bit     │ Header integritás                                       │
└───────────┴───────────┴─────────────────────────────────────────────────────────┘
                                                                        Σ = 128 bit
```

A `TActorRef` 64 bit **bit-azonos** a header következő mezőivel:
- `core-coord[24]` ↔ `dst[24]`
- `actor-id[8]` ↔ `dst_actor[8]`
- `perms[8]` ↔ `perms[8]`
- `HMAC[24]` ↔ `HMAC[24]`

**Send műveletkor a runtime az alsó 64 bitet 1:1 átemeli a header-be**, konverzió nélkül.

---

## 5. döntés: HMAC algoritmus és védelmi piramis

### 5.a) Elvetett: HMAC-SHA256 truncate

~80 cycle HW verify, ~30k gate area. **Felesleges nagyság** rövid üzenet MAC-ra.

### 5.b) Végső: SipHash-128 MSB-truncate 24 bit-re

| Tulajdonság | Érték | Indok |
|---|---|---|
| Algoritmus | SipHash-128 | Specifikusan rövid üzenet MAC-ra tervezett (Aumasson & Bernstein) |
| Truncate | felső 24 bit | NIST SP 800-107 konvenció (MSB) |
| HW area | ~5k gate / verify unit | 6× kisebb, mint a HMAC-SHA256 |
| Verify ciklus | ~10 cycle @ 500 MHz = 20 ns | 8× gyorsabb, mint a HMAC-SHA256 |
| Forgery resistance | 1 : 16,8 millió (önmagában) | A védelmi piramissal együtt értékelendő |

### 5.c) Védelmi piramis (defense-in-depth)

A 24 bit HMAC önmagában **nem elegendő** — egy ötrétegű piramis védelem támogatja:

```
┌────────────────────────────────────────────────────────────────────┐
│ 1. Compile-time:                                                   │
│    AuthCode SHA-256(bytecode) ↔ cert.PkHash binding                │
│    BitIce PQC aláírás                                              │
│    cert.SubjectId aláírói identitás                                │
├────────────────────────────────────────────────────────────────────┤
│ 2. Boot-time:                                                      │
│    Seal Core verify (BitIce trust chain)                           │
│    eFuse.CaRootHash (FenySoft master key SHA, OTP)                 │
│    revocation_list check (cert.SubjectId)                          │
├────────────────────────────────────────────────────────────────────┤
│ 3. Spawn-time:                                                     │
│    capability_registry (M2.5): aláíró-blacklist check              │
│    Bytecode SHA blacklist check                                    │
├────────────────────────────────────────────────────────────────────┤
│ 4. Send-time:                                                      │
│    HMAC verify (célcore mailbox-edge HW unit, osreq-007)           │
│    perms verify (capability bit-flag)                              │
├────────────────────────────────────────────────────────────────────┤
│ 5. Quarantine trigger (hibás HMAC esetén):                         │
│    HW fail-stop: küldő core supervisor IRQ + drop                  │
│    AuthCode quarantine: cert.SubjectId revocation_list-be          │
│    Bytecode SHA blacklist-be                                       │
│    Futó instance-ok az adott aláírótól: supervisor terminate       │
│    Per-chip hibás-HMAC counter increment → threshold átlépve:      │
│      chip-szintű kulcsrotáció (capability_registry) + alarm        │
└────────────────────────────────────────────────────────────────────┘
                                      │
                              7. Alapelvek:
                              Shared-nothing per-core SRAM isolation
                              Quench-RAM SEAL/RELEASE
```

---

## Brute-force költségelemzés realisztikus tempóval

A korábbi pesszimista becslések (88k core × 1 GHz × 1 verify/cycle = 10¹³ próba/sec) **abszurd felső plafonok**. A realisztikus számok:

### Tempó-korlátok

```
SipHash verify HW unit:        10 cycle / verify @ 500 MHz = 20 ns
Per célcore mailbox-edge:      sequential, ~5 × 10⁷ verify/sec MAX (verify bottleneck)
Cross-region roundtrip:        2 × 139 cycle + verify ≈ 600 ns @ 500 MHz
Hot-spot backpressure:         88k core 1 célre = mailbox FIFO 8–64 mély telítődik,
                               87 936 core blokkolódik
```

A támadónak **válaszra kell várnia** ahhoz, hogy tudja, melyik HMAC volt sikeres (a Symphact shared-nothing modellben nincs side-channel az aktorok között). Tempó: **~10⁶ próba/sec** aggregate.

### Bytecode-szintű gating (Open mode)

Még ha a támadó megpróbálná megkerülni az aláírói blacklist-et új cert-ekkel, **minden új próba új bytecode + új cert + új betöltés**:

| Lépés | Idő |
|---|---|
| Random HMAC tag a bytecode payload-jába | < 1 ns |
| Bytecode újraépítés (új SHA-256) | ~1 ms |
| PQC self-signing (új cert) | ~10–100 ms |
| Bytecode + cert betöltése a chipre | **~1–10 sec** ← bottleneck |
| AuthCode Seal Core verify | ~1 ms |
| Spawn aktor + Send + roundtrip | ~1 ms |

**Per próba teljes idő (Open mode): ~1–10 másodperc**.

### Brute-force idő-táblázat

| HMAC bit | Keyspace | Strict mode (FenySoft KYC, ~3 nap/aláíró) | Open mode (~1 sec/próba) |
|---|---|---|---|
| **24** (választott) | 1,7 × 10⁷ | **~70 ezer év** | ~6 hónap–5 év |
| 32 | 4,3 × 10⁹ | ~36 millió év | ~140 év |
| 48 | 2,8 × 10¹⁴ | ~2 billió év | ~9 millió év |
| 64 | 9,2 × 10¹⁸ | ~10¹⁷ év | ~290 milliárd év |

Plusz **storage saturation** védelem: 16,8M próba × 32 byte SHA = 537 MB blacklist tárolásigény, ami a chip on-chip blacklist tárhely (kilobyte–megabyte) sokszorosa → a támadás a sikeres találat előtt megáll, és a `MEM_FULL` admin riasztást ad.

### Konklúzió

A **24 bit HMAC + védelmi piramis** kombinációja:
- **Strict mode**: ~70 ezer év brute-force védelem (FenySoft Strict whitelist)
- **Open mode**: ~6 hónap–5 év brute-force védelem (csak fejlesztői chipeken volna releváns, **NEM létezik a FenySoft termékben** — lásd `trust-model-hu.md`)

Mindkét szint **post-quantum-szintű biztonság a Symphact célközönségére** (consumer, IoT, enterprise, ipari kritikus). Nemzetállami szintű támadásra a Secure Edition (F6.5) ad külön védelmet (`CLI-CPU/docs/secure-element-hu.md`).

---

## Production deployment trust modell

A 24 bit HMAC védelmi szintje a **gating function** integritásától függ. A FenySoft termékvonal **kötelezően Strict whitelist** módban szállítódik, FenySoft-controlled aláírói pool-lal:

- Az `eFuse.CaRootHash` egyetlen slotot tartalmaz, **OTP**, FenySoft master key SHA-jával programozva
- Multi-root array, Open-mode bit, deployment-mode toggle: **NEM támogatott** (támadási vektor lenne)
- Vásárlói bytecode-aláírás csak FenySoft KYC + audit folyamaton keresztül

Részletes üzleti modell, NEM-támogatott opciók indoklása, és multi-tier árképzés: [`trust-model-hu.md`](trust-model-hu.md).

---

## Mit jelent ez a mai kódra (invariánsok)

A 64 bites kontraktus tartása **két szabályt** köt meg:

1. **Tilos** olyan kódot írni, ami feltételezi, hogy `ActorId = 1, 2, 3, …` szekvenciális. Pl. `for (long i = 0; i < count; i++)` ref iteráció **anti-pattern** — a jövőbeli ref-ek "lyukasak" lesznek (HMAC random, core-coord sparse).
2. **Tilos** a `long` típusból kilépni — nem `string ActorName`, nem `Guid`, nem két mezős rekord. A 64 bit a szerződés.

**Tesztekben sem szabad** szekvenciális ID-kre építeni. Helyes minta: a `TActorSystem.Spawn` által visszaadott ref-et használni.

---

## Nyitott kérdések

1. **HMAC counter threshold értéke** — javaslat: 16 (false positive minimum, brute-force korlátozás maximum). osreq-007 része, CFPU csapat egyetértésével.
2. **Per-core HMAC kulcs rotációs frekvencia** — automatikus rotáció a counter threshold átlépésére (event-driven), vagy ütemezett (időzített)? Javaslat: event-driven, mert időzített rotáció nem ad többet, csak komplexitást.
3. **Inter-chip HMAC algoritmus** — ugyanaz SipHash-128, vagy erősebb (HMAC-SHA256)? A chip-en kívüli támadási profil szigorúbb, érdemes lehet erősebb. osreq-006 része.
4. **Proxy aktor megosztott use** — chip A 100 aktora chip B-re küld: ugyanaz a `remote_proxy(chip B)`, vagy minden küldő külön? Megosztás egyszerűbb, de back-pressure problémát hozhat.
5. **Ref szivárgás chiphatáron** — chip A aktor átadja egy chip A-belüli ref-jét chip B aktornak: a runtime észreveszi és új proxy-t kreál chip B-n, vagy tilos?

---

## Kapcsolódó tervek és milestone-ok

- **`CLAUDE.md`** — a 64 bites public surface kontraktus rögzítése
- **`docs/trust-model-hu.md`** — FenySoft Strict whitelist üzleti modell, NEM-támogatott opciók
- **`docs/osreq-to-cfpu/osreq-007-actor-ref-format-hu.md`** — HW követelmények (header v2.5, mailbox-edge HMAC verify unit, counter, fail-stop, single-root eFuse)
- **`docs/roadmap-hu.md` M0.6 — Remoting** — proxy-aktor minta első iterációja
- **`docs/roadmap-hu.md` M0.7 — CFPU Hardware Integration** — végleges bit-elrendezés szilíciumon, MMIO mailbox integráció
- **`docs/roadmap-hu.md` M2.5 — Capability Registry** — kernel-szintű HMAC kulcskezelés és kibocsátás, AuthCode integráció
- **`docs/vision-hu.md`** — capability-based security és location transparency design alapok
- **`CLI-CPU/docs/architecture-hu.md`** — 24 bites HW cím, Actor címzés szoftveres dispatch
- **`CLI-CPU/docs/interconnect-hu.md`** — cella header struktúra, tree-topology, backpressure
- **`CLI-CPU/docs/ddr5-architecture-hu.md`** — CAM tábla aktor-szintű memóriajogosultság
- **`CLI-CPU/docs/authcode-hu.md`** — AuthCode trust chain, SHA-256 binding, revocation
- **`CLI-CPU/docs/quench-ram-hu.md`** — SEAL/RELEASE invariáns, cold-boot védelem
- **`CLI-CPU/docs/security-hu.md`** — eliminált CWE-k

---

## Verziótörténet

| Verzió | Dátum | Változás |
|---|---|---|
| 1.0 | 2026-04-25 | Első verzió: 16 bit core-coord (alulméretezett), 28 bit HMAC, proxy minta |
| 2.0 | 2026-04-25 | **Véglegesített specifikáció**: 64 bit ref, `[HMAC:24][perms:8][actor-id:8][core-coord:24]`, CLI-CPU 16 byte header bit-azonos, threat model szekció, brute-force költségelemzés realisztikus tempóval, védelmi piramis, FenySoft Strict whitelist hivatkozás, NEM-támogatott opciók indoklása |
