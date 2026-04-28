# Aktor referencia skálázódás és védelmi modell — Symphact

> English version: [actor-ref-scaling-en.md](actor-ref-scaling-en.md)

> Version: 3.0

> Státusz: véglegesített specifikáció, M0.6 / M0.7 / M2.5 alapja

Ez a dokumentum a `TActorRef` **véglegesített bit-elrendezését**, **wire-formátumát**, és a **védelmi piramist** rögzíti. A v3.0 **MAJOR változás**: a korábbi HMAC-alapú modellt a **CST (Capability Slot Table)** modell váltja — a szoftver nem látja a nyers dst/src címeket, csak egy opaque CST indexet.

> **Célközönség:** Symphact runtime fejlesztők, CFPU HW tervezők (sister repo: `FenySoft/CLI-CPU`), API kontraktus felülvizsgálatakor, biztonsági auditok.

---

## Kontextus

A `TActorRef` a Symphact publikus API-jának **alapköve**: minden aktor-aktor kommunikáció ezen keresztül történik. A `CLAUDE.md` rögzíti az API kontraktust:

```csharp
public readonly record struct TActorRef(int SlotIndex);   // 32 bit, opaque CST index, public
```

A 32 bit egy **CST (Capability Slot Table) index** — a szoftver számára opaque, a HW oldja fel futásidőben a tényleges dst/src címekre. A felhasználó a runtime-belüli reprezentáció **belsejébe** nem nyúl — az `int` opaque token, csak `Equals`/`GetHashCode`/Send-paraméter szempontból érdekes.

A korábbi spec (v2.0) 64 bites `long ActorId`-t írt elő `[HMAC:24][perms:8][actor-id:8][core-coord:24]` felépítéssel. A v3.0 **megszünteti az HMAC modellt** és a szoftveres bit-elrendezést: a CST HW tábla tartalmazza a cél címet, aktor ID-t és jogosultságokat.

---

## Threat model

A Symphact védelmi modellje **szoftveres + supply-chain** támadásokra fókuszál. A fizikai szintű támadás **out-of-scope** ezen a védelmi rétegen.

| Támadási vektor | Védelem | Hatókör |
|---|---|---|
| Szoftveres jogosultsághamisítás (hamis CST index) | CST HW lookup + SEAL védelem (érvénytelen index = trap) | **In-scope** |
| Másik aktor olvassa egy aktor CST tábláját | Shared-nothing per-core QSRAM isolation + SEAL | **In-scope** |
| Kompromittált aláíró (rosszindulatú szoftver) | AuthCode + supply-chain quarantine | **In-scope** |
| Jogosultság-eszkaláció (perms manipuláció) | Perms a CST-ben, HW-only írás, szoftver nem módosíthatja | **In-scope** |
| Cold boot (kikapcsolás után fagyasztás) | Quench-RAM RELEASE atomi wipe (mellékhatás) | **In-scope** |
| **Bus-MITM, chip-decap, FIB-szondázás** | — | **Out-of-scope** (fizikai hozzáférés = SSD ellopás egyszerűbb) |
| **Side-channel (timing, power)** | — | **Out-of-scope** alapszinten (Secure Edition F6.5-re tartozik) |

Ez a Linux/Windows/macOS szintű commercial threat model, és csak a Secure Element (F6.5) megy tovább a fizikai védelemre. Production deployment-eknél ez **explicit dokumentumban rögzítendő** (lásd [`trust-model-hu.md`](trust-model-hu.md)).

> **seL4 analógia:** A CST modell koncepcionálisan az seL4 capability-based security rendszeréhez hasonlít — a szoftver capability handle-eket (slot index) kap, amelyek egy HW/kernel-szintű capability táblán át oldódnak fel. A szoftver nem látja és nem módosíthatja a tényleges jogosultsági bejegyzéseket.

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

A **24 bites core-coord** (16M core) a headerben elegendő F-9 generációkig — a CLI-CPU `architecture-hu.md:1320` 24-bites HW címével pontosan egyezik. A szoftver-oldali `TActorRef` ezt **nem tartalmazza közvetlenül** — a CST HW tábla feloldja.

---

## 1. döntés: A `TActorRef` 32 bit CST index

### 1.a) Elvetett: 64 bites `long` HMAC-alapú ref (v2.0)

A korábbi `TActorRef(long ActorId)` 64 biten kódolta az összes információt: `[HMAC:24][perms:8][actor-id:8][core-coord:24]`. Ez a szoftver kezébe adta a HW cím és jogosultság bitjeit.

**Miért vetettük el:**
- A szoftver számára látható perms/HMAC bitek **támadási felületet** jelentenek
- Az HMAC kriptográfiai verify **area-igényes** (~5k gate / verify unit) és **latencia-költséges** (~10 cycle)
- A CST modell 1 ciklusú HW lookup-pal helyettesíti, kriptográfiai overhead nélkül

### 1.b) Elvetett: 160 bites struct (`vision-en.md` korábbi változata)

Egy 4 mezős struct (CoreId int + MailboxIndex int + CapabilityTag long + Permissions int) komplexitást ad a felhasználóhoz, és a HW interconnect fejléc-mérete **nő**.

**Miért vetettük el:**
- A capability **opaque token** kell legyen — a fejlesztő ne tudjon a tartalmáról
- 160 bit verbatim átvitele a tree fabric-on ~5–10% area növekedést ad

### 1.c) Végső döntés: `TActorRef(int SlotIndex)` — 32 bit, opaque CST index

```csharp
public readonly record struct TActorRef(int SlotIndex);   // 32 bit, opaque
```

A `SlotIndex` egy per-core CST (Capability Slot Table) bejegyzésre mutat. A szoftver:
- **NEM látja** a cél core címét, actor ID-t, vagy jogosultságokat
- **NEM módosíthatja** a CST bejegyzést (HW-only írás, SEAL-védett QSRAM)
- Csak **Send(ref, msg)** hívást tehet — a HW lookup feloldja a CST → header mapping-et

A `TActorRef` chip-local — chip-en kívüli címzés proxy-aktor mintával (lásd 3. döntés).

---

## 2. döntés: CST (Capability Slot Table) modell

A korábbi v2.0 megoldás (`[HMAC:24][perms:8][actor-id:8][core-coord:24]` szoftveres bit-elrendezés) **elavult**. Helyette a HW-managed CST modell lép érvénybe.

### 2.a) Elvetett: szoftveres HMAC-alapú bit-elrendezés (v2.0)

A `TActorRef` 64 biten hordozta az összes információt. **Problémák:**
- Szoftveres brute-force ellen kriptográfiai védelemre volt szükség (SipHash-128, area/latency overhead)
- A `perms` bit-flag a refben → szoftveresen manipulálható (bár a HW is ellenőrizte)
- A `core-coord` a refben → a szoftver lát HW topológia információt (NEM opaque)

### 2.b) Végső döntés: HW-managed CST QSRAM-ban, SEAL-védett

A CST egy per-core QSRAM tábla, amelyet a HW kezel. Egy entry 8 byte, aligned:

```
 63                                40 39    32 31    24 23          0
┌──────────────────────────────────────┬────────┬────────┬──────────┐
│            reserved                  │ perms  │actor-id│   dst    │
│             24 bit                   │  8 bit │  8 bit │  24 bit  │
└──────────────────────────────────────┴────────┴────────┴──────────┘
                                                          Σ = 64 bit (8 byte)
```

| Mező | Szélesség | Indoklás |
|---|---|---|
| `dst` | 24 bit | Cél core HW cím. = CLI-CPU `dst[24]` az interconnect headerben. 16M core → F-9+ generációkra elegendő |
| `actor-id` | 8 bit | Cél aktor ID. 256 actor/core — egy core-on tipikusan 1–100 aktor él (SRAM-korlát) |
| `perms` | 8 bit, **bit-flag** | Send / Watch / Stop / Delegate / DelegateOnce / Migrate / MaxPri[2] |
| `reserved` | 24 bit | Jövőbeli bővítésre fenntartva (nullázott) |

**CST jellemzők:**
- **QSRAM tárolás**: a CST SEAL-védett Quench-RAM-ban él, cold-boot védett
- **HW-only write**: a szoftver nem írhat közvetlenül a CST-be — csak a supervisor kernel kérhet CST bejegyzés létrehozást/törlést HW trap-en keresztül
- **1 ciklusú lookup**: Send-kor a HW a `SlotIndex`-et 1 órajelciklusban feloldja → dst, actor-id, perms
- **Nincs kriptográfiai overhead**: nem kell HMAC verify, nem kell SipHash, nem kell kulcskezelés

### 2.c) Permission bitek

| Bit | Flag | Jelentés |
|---|---|---|
| 0 | Send | Üzenet küldése a cél aktornak |
| 1 | Watch | Cél aktor életciklus-figyelés |
| 2 | Stop | Cél aktor leállítása |
| 3 | Delegate | CST slot továbbadása (új slot, szűkített perms) |
| 4 | DelegateOnce | Egyszeri delegálás (automatikus revoke) |
| 5 | Migrate | Cél aktor áthelyezése másik core-ra |
| 6–7 | MaxPri[2] | Maximális üzenet-prioritás (0–3), HW-enforced |

### 2.d) Delegation mechanizmus

A CST delegation **supervisor-to-supervisor VN0** csatornán történik:

```
Supervisor A (core 17)
    │  DelegateRequest { OrigSlot, TargetCore, RequestedPerms }
    │  → VN0 (management virtual network)
    ▼
Supervisor B (core 42)
    │  new_perms = RequestedPerms AND OriginalPerms   ← HW szűkít
    │  CST.Allocate(new_entry) → új SlotIndex
    ▼
Válasz: TActorRef(newSlotIndex)   ← target core CST-jében
```

**Invariáns:** `new_perms ⊆ original_perms` — a HW AND művelet biztosítja, hogy delegálással **soha nem lehet jogosultságot bővíteni** (seL4 capability monotonicity).

---

## 3. döntés: Chip-en kívüli címzés (inter-chip kommunikáció)

A 24 bites `dst` mező **egy chip belső címtere**. Multi-chip fabric esetén proxy-aktor minta:

### 3.a) Elvetett: flat globális core-id

20+ bites flat core-id a CST táblát feleslegesen nagyra növeli. A flat címtér **nem létező absztrakció** — a fizikai topológia hierarchikus.

### 3.b) Elvetett: hierarchikus cím a ref-ben

`[chip-id:8][core-id:16][...]` típusú felosztás csak elhalasztja a problémát, és a felhasználó kezdi értelmezni a ref-et (NEM opaque).

### 3.c) Végső: proxy-aktor minta

A `vision-hu.md` location transparency:

> Az aktorok futás közben áthelyezhetők core-ok között — ugyanaz a `Send(actor_ref, msg)` működik minden esetben, és a router (hardveres + szoftveres) eldönti, hová kerül az üzenet.

A `TActorRef` mindig **chip-local** (CST index a helyi core CST-jébe). Egy chipen kívüli aktorhoz a kommunikáció **proxy-aktoron keresztül** megy:

```
Alkalmazás aktor (chip A, core 17)
        │
        │  Send(remoteProxyRef, msg)    ← lokális CST index
        ▼
[remote_proxy aktor] (chip A, core 0)   ← belső state-ben:
        │                                  { TargetChipId, TargetCoreId,
        │                                    TargetActorId, … }
        │  inter-chip link write (osreq-006)
        ▼
   Másik chip → célaktor
```

**Erősségek:**
- A 32 bit CST index örökre elég (chip-local jelentés)
- A proxy-aktor belső state tartalmazza az inter-chip routing információt
- Location transparency: az alkalmazás aktor csak ref-eket lát
- Bevált minta: Akka.Remote, Erlang/OTP distributed, Pony ORCA

**Implementációs csomópontok:**
- M0.6 Remoting milestone: első proxy minta szoftveres TCP transport-tal
- M0.7 CFPU HW Integration: a proxy `inter-chip link` MMIO-ra (osreq-006) áll át
- A proxy aktor maga is supervised (M0.3) — crash esetén újraindul

---

## 4. döntés: Wire-formátum — CLI-CPU interconnect header v3.0

A CLI-CPU interconnect v3.0 header-struktúra:

```
┌───────────┬───────────┬─────────────────────────────────────────────────────────┐
│   Mező    │ Szélesség │                       Jelentés                          │
├───────────┼───────────┼─────────────────────────────────────────────────────────┤
│ dst       │ 24 bit    │ Cél core HW cím                                         │
│ dst_actor │ 8 bit     │ Cél aktor ID (max 256 aktor / core)                     │
│ src       │ 24 bit    │ Forrás core HW cím (HW által kitöltött, nem hamisítható)│
│ src_actor │ 8 bit     │ Küldő aktor ID                                          │
│ seq       │ 16 bit    │ Sorszám (fragmentált üzenetek sorrendje)                │
│ flags     │ 8 bit     │ VN0/VN1, relay flag                                     │
│ len       │ 8 bit     │ Payload byte-szám                                       │
│ reserved  │ 8 bit     │ Fenntartott (nullázott)                                 │
│ CRC-16    │ 16 bit    │ Header integritás (16 bit)                              │
│ CRC-8     │ 8 bit     │ Header CRC kiegészítés                                  │
└───────────┴───────────┴─────────────────────────────────────────────────────────┘
                                                                        Σ = 128 bit
```

**Változások a v2.5 header-hez képest:**
- **HMAC mező törölve** — nincs szükség kriptográfiai verify-re, a CST HW lookup helyettesíti
- **perms mező törölve a headerből** — a jogosultságok a CST-ben élnek, a HW a Send-kor ellenőrzi
- **seq mező 16 bit-re bővült** (8-ról) — nagyobb fragmentációs tartomány
- **CRC-16 + CRC-8** — erősebb header integritás-védelem (a CRC-8 megmaradt kompatibilitásra)

**A `TActorRef` (CST index) NEM bit-azonos a headerrel.** A HW a Send-kor:
1. CST lookup: `SlotIndex` → `{dst, actor-id, perms}`
2. Perms ellenőrzés: ha a Send flag nincs beállítva → trap
3. Header kitöltés: `dst`, `dst_actor` a CST-ből; `src`, `src_actor` a HW tölti ki automatikusan

---

## 5. döntés: CST védelmi modell (korábbi HMAC algoritmus helyett)

### 5.a) Elvetett: SipHash-128 HMAC verify (v2.0)

A korábbi modell SipHash-128 MSB-truncate 24 bit-re algoritmust használt per-message HMAC verify-re.

**Miért vetettük el:**
- ~5k gate / verify unit area igény **minden célcore-on**
- ~10 cycle / verify latency minden üzenetnél
- Kulcskezelés komplexitása (rotáció, per-core kulcsok, counter threshold)
- A CST modell **ugyanazt a védelmet adja, kriptográfiai költség nélkül**

### 5.b) Végső döntés: CST HW lookup + SEAL védelem

| Tulajdonság | Érték | Indoklás |
|---|---|---|
| Védelmi mechanizmus | CST QSRAM lookup | HW-managed, szoftver nem módosíthatja |
| Lookup ciklus | **1 cycle** @ 500 MHz = 2 ns | 10× gyorsabb, mint a korábbi SipHash verify |
| HW area | ~1k gate (index decoder + comparator) | 5× kisebb, mint a SipHash verify unit |
| Érvénytelen index | HW trap (supervisor IRQ + drop) | Fail-stop, mint korábban |
| Perms enforce | CST entry perms AND művelet | Azonos ciklusban, a lookup részeként |

### 5.c) Védelmi piramis (defense-in-depth) — v3.0

A CST modell **nem kriptográfiai**, de ötrétegű piramis védi:

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
│    CST slot allokáció: supervisor HW trap-en keresztül             │
├────────────────────────────────────────────────────────────────────┤
│ 4. Send-time:                                                      │
│    CST HW lookup: SlotIndex → {dst, actor-id, perms} (1 ciklus)   │
│    Érvénytelen index → HW trap + drop                              │
│    perms verify: szükséges flag hiánya → HW trap + drop            │
├────────────────────────────────────────────────────────────────────┤
│ 5. Quarantine trigger (érvénytelen CST access esetén):             │
│    HW fail-stop: küldő core supervisor IRQ + drop                  │
│    AuthCode quarantine: cert.SubjectId revocation_list-be          │
│    Bytecode SHA blacklist-be                                       │
│    Futó instance-ok az adott aláírótól: supervisor terminate       │
│    Per-chip érvénytelen-CST counter → threshold → alarm            │
└────────────────────────────────────────────────────────────────────┘
                                      │
                              Alapelvek:
                              Shared-nothing per-core SRAM isolation
                              Quench-RAM SEAL/RELEASE
                              CST QSRAM: SEAL-védett, HW-only write
```

**Miért elegendő kriptográfia nélkül:**
- A szoftver **soha nem látja** a tényleges dst címet vagy perms-et — csak CST indexet
- A CST QSRAM **SEAL-védett** — szoftveres írás fizikailag nem lehetséges
- Érvénytelen CST index → azonnali HW trap (nem próba-hiba, hanem fail-stop)
- Nincs brute-force támadási vektor: a CST index vagy valid (legális spawn eredménye), vagy érvénytelen (azonnali trap)

---

## Production deployment trust modell

A CST védelmi szintje a **SEAL QSRAM** integritásától függ. A FenySoft termékvonal **kötelezően Strict whitelist** módban szállítódik:

- Az `eFuse.CaRootHash` egyetlen slotot tartalmaz, **OTP**, FenySoft master key SHA-jával programozva
- Multi-root array, Open-mode bit, deployment-mode toggle: **NEM támogatott** (támadási vektor lenne)
- Vásárlói bytecode-aláírás csak FenySoft KYC + audit folyamaton keresztül
- CST slot allokáció kizárólag supervisor kernelen keresztül, HW trap-pel

Részletes üzleti modell, NEM-támogatott opciók indoklása, és multi-tier árképzés: [`trust-model-hu.md`](trust-model-hu.md).

---

## Mit jelent ez a mai kódra (invariánsok)

A 32 bites CST index kontraktus **két szabályt** köt meg:

1. **Tilos** olyan kódot írni, ami feltételezi, hogy `SlotIndex = 0, 1, 2, …` szekvenciális. A CST slot allokáció implementáció-függő — a jövőbeli ref-ek "lyukasak" lehetnek (deallokált slot-ok, újrahasználat).
2. **Tilos** az `int` típusból kilépni — nem `string ActorName`, nem `Guid`, nem két mezős rekord. A 32 bit CST index a szerződés.

**Tesztekben sem szabad** szekvenciális indexekre építeni. Helyes minta: a `TActorSystem.Spawn` által visszaadott ref-et használni.

---

## Nyitott kérdések

1. **CST méret per core típus** — Nano core (4 KB SRAM): hány CST slot fér el? Actor core: hány? A QSRAM particionálás CST vs. aktor-adat arányát rögzíteni kell.
2. **CST slot garbage collection** — automatikus felszabadítás aktor-halál esetén (supervisor-driven), vagy explicit revoke? Javaslat: supervisor-driven automatikus, explicit revoke opcionális.
3. **Proxy aktor megosztott use** — chip A 100 aktora chip B-re küld: ugyanaz a `remote_proxy(chip B)`, vagy minden küldő külön? Megosztás egyszerűbb, de back-pressure problémát hozhat.
4. **Ref szivárgás chiphatáron** — chip A aktor átadja egy chip A-belüli ref-jét chip B aktornak: a runtime észreveszi és új proxy-t kreál chip B-n, vagy tilos?
5. **CST overflow kezelés** — ha egy core CST-je megtelik, a supervisor hogyan reagáljon? Javaslat: back-pressure a spawn-ra (várakozás vagy trap a kérőnek).

---

## Kapcsolódó tervek és milestone-ok

- **`CLAUDE.md`** — a 32 bites CST index public surface kontraktus rögzítése
- **`docs/trust-model-hu.md`** — FenySoft Strict whitelist üzleti modell, NEM-támogatott opciók
- ~~**`docs/osreq-to-cfpu/osreq-007-actor-ref-format-hu.md`**~~ — **OBSOLETE** (HMAC-alapú header v2.5, mailbox-edge HMAC verify unit, counter, fail-stop — a CST modell felváltja)
- **`docs/roadmap-hu.md` M0.6 — Remoting** — proxy-aktor minta első iterációja
- **`docs/roadmap-hu.md` M0.7 — CFPU Hardware Integration** — végleges CST implementáció szilíciumon, MMIO mailbox integráció
- **`docs/roadmap-hu.md` M2.5 — Capability Registry** — kernel-szintű CST slot kezelés és kibocsátás, AuthCode integráció
- **`docs/vision-hu.md`** — capability-based security és location transparency design alapok
- **`CLI-CPU/docs/architecture-hu.md`** — 24 bites HW cím, Actor címzés szoftveres dispatch
- **`CLI-CPU/docs/interconnect-hu.md`** — cella header struktúra (v3.0), tree-topology, backpressure
- **`CLI-CPU/docs/ddr5-architecture-hu.md`** — CAM tábla aktor-szintű memóriajogosultság
- **`CLI-CPU/docs/authcode-hu.md`** — AuthCode trust chain, SHA-256 binding, revocation
- **`CLI-CPU/docs/quench-ram-hu.md`** — SEAL/RELEASE invariáns, cold-boot védelem
- **`CLI-CPU/docs/security-hu.md`** — eliminált CWE-k

---

## Changelog

| Verzió | Dátum | Összefoglaló |
|--------|-------|-------------|
| 3.0 | 2026-04-28 | **MAJOR**: HMAC modell megszüntetve, CST (Capability Slot Table) modell bevezetése. `TActorRef(long ActorId)` 64 bit → `TActorRef(int SlotIndex)` 32 bit. SipHash/HMAC verify → 1 ciklusú CST HW lookup. Perms a headerből a CST-be. Header v3.0 (HMAC/perms mező törölve, seq 16 bit, CRC-16). Brute-force elemzés törölve (nem releváns). osreq-007 OBSOLETE. Delegation: supervisor-to-supervisor VN0, AND szűkítés. seL4 capability analógia. |
| 2.0 | 2026-04-25 | Véglegesített specifikáció: 64 bit ref, `[HMAC:24][perms:8][actor-id:8][core-coord:24]`, CLI-CPU 16 byte header bit-azonos, threat model, brute-force költségelemzés, védelmi piramis |
| 1.0 | 2026-04-25 | Első verzió: 16 bit core-coord (alulméretezett), 28 bit HMAC, proxy minta |
