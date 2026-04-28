# Trust modell és FenySoft Strict whitelist üzleti modell

> English version: [trust-model-en.md](trust-model-en.md)

> Version: 1.1

> Státusz: véglegesített deployment policy, F6-Silicon One és későbbi tape-out-ok alapja

Ez a dokumentum rögzíti a Symphact / CFPU termékvonal **trust modelljét**: ki bocsát ki bytecode-aláírókat, hogyan jutnak a vásárlók aláírói identitáshoz, és **mely opciók nem támogatottak** biztonsági okokból.

> **Célközönség:** FenySoft termékmenedzsment, vásárlói partnerek (chip-integrátorok, OEM-ek), security auditok, jogi review.

---

## A trust modell tömör összefoglalója

A FenySoft által gyártott CFPU chipek **kötelezően Strict whitelist** módban szállítódnak. Egyetlen trust anchor (FenySoft master key), egyetlen aláírói pool (FenySoft-controlled), egyetlen revocation lista. Nincs runtime konfigurációs override, nincs developer-mode toggle, nincs multi-root opció.

```
Gyártás (FenySoft):
   eFuse.CaRootHash := SHA-256(FenySoft master public key)
                       ↑
                       OTP, EGYETLEN slot, fizikailag módosíthatatlan a kibocsátás után

Életmód:
   Minden bytecode FenySoft-aláírt vagy delegáltan-aláírt cert-tel.
   Érvénytelen CST slot = aláíró azonnal blacklist-be (issuer-trust quarantine).
```

Ez a modell biztosítja a CST (Capability Slot Table) HW-managed capability modell integritását.

---

## A trust chain a gyártástól a futás közbeni verifikációig

```
1. CFPU gyártás (FenySoft):
   ┌──────────────────────────────────────────────────────────────┐
   │  Tape-out / packaging:                                       │
   │    eFuse.CaRootHash := SHA-256(FenySoft_root_public_key)     │
   │  ⇒ Ez a chip ÉLETTARTAMÁRA fix.                              │
   │  ⇒ FenySoft master HSM-ben őrzi a private root key-t.        │
   └──────────────────────────────────────────────────────────────┘

2. Vásárló bytecode aláírást igényel:
   ┌──────────────────────────────────────────────────────────────┐
   │  Vásárló → FenySoft:                                         │
   │    - bytecode SHA-256                                        │
   │    - vásárlói azonosító (cég, projekt)                       │
   │    - KYC dokumentumok                                        │
   │                                                              │
   │  FenySoft (KYC + security audit):                            │
   │    - Vásárló identitásának ellenőrzése                       │
   │    - Bytecode review (statikus elemzés, opkód whitelist)     │
   │    - Aláírás:                                                │
   │      cert = sign(FenySoft_HSM, {                             │
   │        PkHash    = SHA-256(bytecode),                        │
   │        SubjectId = vásárló-azonosító                         │
   │      })                                                      │
   │                                                              │
   │  FenySoft → Vásárló: cert (CIL bináris mellé)                │
   └──────────────────────────────────────────────────────────────┘

3. Vásárló a chipre tölti (bytecode + cert).

4. Seal Core verify (boot-time):
   ┌──────────────────────────────────────────────────────────────┐
   │  1. SHA-256(bytecode) == cert.PkHash ?           ← binding   │
   │  2. BitIce.Verify(cert, eFuse.CaRootHash) ?      ← trust ←┐  │
   │  3. cert.SubjectId ∉ revocation_list ?           ← quar.  │  │
   │                                                           │  │
   │  Mind OK → CODE régió SEAL → Spawn engedélyezett          │  │
   └───────────────────────────────────────────────────────────┘  │
                                                                  │
   Trust anchor: FenySoft root key — kizárólag FenySoft delegálhat ┘
```

---

## Üzleti modell-opciók

A FenySoft a vásárlóknak **multi-tier** árképzést kínálhat:

| Tier | Mit kap a vásárló | Árazási alap |
|---|---|---|
| **Per-cert** | Egyszeri cert kibocsátás konkrét bytecode-hoz | $100-1000 / cert |
| **Subscription** | Éves fejlesztői előfizetés, korlátlan cert egy app-családra | $500-5000 / év |
| **Enterprise CA delegation** | Subordinate CA — vásárló saját CA-t kap, FenySoft chain alatt | $50k-500k / év (compliance audit-tal) |
| **Compliance tier** | Audited + supported license, jogi felelősséggel (ISO 26262, IEC 61508, FIPS 140-3) | $500k+ / év |

A megfelelő tier függ a vásárló volumenétől, compliance-igényétől, és üzleti modell-jétől:

- **IoT / consumer eszköz gyártó**: per-cert vagy subscription
- **Enterprise app fejlesztő**: subscription
- **Automotive Tier 1, ipari gyártó**: enterprise CA delegation
- **Pénzintézet, telekomm, energia**: compliance tier

---

## NEM-támogatott opciók — biztonsági indoklás

A következő opciókat a FenySoft **explicit elutasítja** a CFPU termékvonalban, mert mindegyik **támadási vektort** teremtene.

### NEM: Multi-root eFuse array

**Mit jelentene:** több `CaRootHash` slot az eFuse-ban (FenySoft + vásárló saját + spare-ek), így a vásárló saját aláírói pool-t hozhatna létre.

**Miért NEM:**
- A spare slot-ok **támadási felület**: egy gyártás utáni támadó (akár garanciaidőtartam alatt) beégethetné a saját root kulcsát egy spare slot-ba, és onnantól bármilyen bytecode-ot futtathatna a chipen
- A "vásárlói root" üzleti előny **NEM ér többet**, mint a hardveres single-point-of-trust biztonsága
- Az enterprise CA delegation tier (subordinate CA) **ugyanazt** szolgáltatást adja, **anélkül** hogy a chip-en több slot kéne

**Helyettesítő megoldás:** Enterprise CA delegation tier — a vásárló saját subordinate CA-t kap, ami a FenySoft chain alatt működik, és teljes körű kontrollt ad az ő aláírói pool-jára.

### NEM: Open-mode eFuse bit (developer-chip vs production-chip)

**Mit jelentene:** egy eFuse bit, ami "developer mode"-ot kapcsolna be, ahol a Seal Core átengedne self-signed bytecode-ot is (FenySoft cert nélkül).

**Miért NEM:**
- Bármilyen runtime-konfigurálható trust decision **támadási vektor**: a támadó beállítja a bitet, és a védelem felfüggesztődik
- A "developer-chip" megkülönböztetés **runtime trust decision**, ami szembemegy a `authcode-hu.md` SHA-256 binding kötelező invariánsával
- Egy fejlesztőnek a megfelelő modell: dedikált developer subscription tier (alacsony árú, gyors KYC), NEM külön chip-konfiguráció

**Helyettesítő megoldás:** Developer subscription tier — alacsony árú, gyors-átfutású FenySoft cert kibocsátás fejlesztői use-case-ekhez.

### NEM: Deployment-mode toggle / runtime override

**Mit jelentene:** valamilyen runtime mechanizmus a trust modell felfüggesztésére (pl. boot-kor egy speciális szignálra, vagy egy "trusted admin" message-re).

**Miért NEM:**
- Ha létezik bármilyen runtime kapcsoló a trust modellhez, az **a támadó célpontja** lesz
- A védelem alapja: **NEM létezik konfiguráció**, amit megkerülni vagy átkapcsolni lehetne
- Az AuthCode `SHA-256(bytecode) ↔ cert.PkHash` binding **kötelező invariáns** — semmilyen runtime mechanizmus nem szakíthatja meg

**Helyettesítő megoldás:** Nincs. A trust modell **statikusan zárt** a chip élettartamára.

### NEM: "Open mode" deployment a FenySoft termékvonalban

**Mit jelentene:** a chip "Open mode"-ban szállítva, ahol bárki bármilyen bytecode-ot futtathat (akár FenySoft cert nélkül, akár self-signed cert-tel).

**Miért NEM:**
- A FenySoft termékmárka **egyenes következménye** a Strict whitelist mode — ez ad megbízhatóságot
- Open mode chip "FenySoft Verified" jelzéssel = a brand integritásának sérülése
- Akinek Open mode kell (pl. teljesen saját ökoszisztéma fejlesztése), az a CLI-CPU nyílt forráskódjából **saját chipet gyárthat** saját root key-vel — ez a CERN-OHL-S licensz lehetősége

**Helyettesítő megoldás:** A vásárló a CLI-CPU open-source RTL-ből saját chipet gyárthat, saját trust anchor-ral. **Ez NEM "FenySoft Verified Symphact chip"** — más termék, más brand, más kategória.

---

## Open-source vs zárt trust chain — a feszültség feloldása

A Symphact Apache-2.0, a CLI-CPU CERN-OHL-S — **nyílt licenszek**. A trust chain zártsága első ránézésre ellentmondás-szerű, de **nem ütközik a licenszekkel**:

| Réteg | Licensz / status | Ki kontrollálja |
|---|---|---|
| Symphact forráskód | Apache-2.0 | Bárki, lefork-olható, módosítható |
| CLI-CPU RTL, ISA spec, szimulátor | CERN-OHL-S | Bárki, lefork-olható, gyártható |
| **Egy konkrét chip eFuse tartalma** | **A gyártó döntése** | A chip gyártója (pl. FenySoft) |
| **"FenySoft Verified" termék-márka** | **Védjegy** | FenySoft kizárólagosan |

Ez **az Android-modell**:
- Az AOSP (Android Open Source Project) Apache-2.0 — bárki használhatja
- A Google Play Services + Google Apps zárt — csak Google-aláírt eszközök
- A gyártóknak Google CTS (Compatibility Test Suite)-en kell átesniük

Ugyanígy a Symphact:
- **Tiszta CFPU** (saját gyártás, saját root): a vásárló saját bytecode-ot futtathat saját aláírással. **Nem "FenySoft Verified"** — más kategória.
- **FenySoft Verified CFPU**: a hivatalos FenySoft termék, FenySoft root-tal, FenySoft cert kibocsátással, FenySoft támogatással.

A vásárló **választhat**:
- **Olcsóbb, gyorsabb, szabadabb**: tiszta CFPU saját gyártásban, saját trust chain
- **Drágább, megbízhatóbb, FenySoft-támogatott**: FenySoft Verified termék

---

## A trust modell mire ad cserébe a fejlesztői friction-ért

A FenySoft Strict whitelist modell **fejlesztői friction-t** generál:

| Friction | Mire használ |
|---|---|
| Új cert kibocsátás napokat-hetes átfutás (KYC, audit) | Brand integritás, reputation, security |
| Vásárlónak FenySoft-tól kell kérnie aláírást | Recurring revenue, central revocation, audit trail |
| Apache-2.0 / CERN-OHL-S nyíltság látszólag korlátozott | Hardware-szintű biztonsági garanciák |

Cserébe a vásárló kapja:

| Garancia | Hatás |
|---|---|
| Hardware-szintű CST védelem | HW-managed capability tábla, FenySoft-controlled gating-gel |
| Central revocation | Egy malware-bytecode-ot a FenySoft globálisan revoke-olhat — minden chipen, azonnal |
| Compliance trail | Minden cert kibocsátás auditálható (GDPR, ISO 26262, IEC 61508, FIPS 140-3) |
| Brand bizalmi pecsét | "FenySoft Verified" minőségjelzés a termékhomlokon |
| Security audit | A FenySoft cert kibocsátás bytecode review-t tartalmaz |

A consumer / IoT / enterprise / ipari kritikus célközönség **többségének** a FenySoft modell **drámai értéket** ad: nem kell saját security infrastruktúrát építenie, a HW-szintű védelem kész és működő.

---

## Az eFuse OTP konfiguráció rögzítése

(**Megjegyzés:** osreq-007 OBSOLETE — a CST modell váltja fel.) A trust anchor szigorítás:

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

Ez a követelmény a CFPU csapatnak (`FenySoft/CLI-CPU` repo), és a F6-Silicon One és későbbi tape-out-okra vonatkozik.

---

## Az F6.5 Secure Edition viszonya a trust modellhez

A `CLI-CPU/docs/secure-element-hu.md` szerint a **Secure Edition (F6.5)** opcionális chip-variánst ad, ami **kiegészíti** (NEM helyettesíti) a Strict whitelist modellt:

- F6 (alap): Strict whitelist + fizikai támadás out-of-scope
- **F6.5 Secure**: Strict whitelist + fizikai tamper resistance + side-channel countermeasures + PUF + TRNG

A Secure Edition **nemzetállami szintű támadásra** ad védelmet (dedikált anti-FIB, anti-decap, mesh, side-channel countermeasures). A trust modell **mindkét variánsban azonos** — csak a fizikai-réteg védelem különbözik.

---

## Kapcsolódó dokumentumok

- **`actor-ref-scaling-hu.md`** — TActorRef bit-elrendezés és védelmi piramis
- ~~**`osreq-to-cfpu/osreq-007-actor-ref-format-hu.md`**~~ — **OBSOLETE** (CST modell váltja fel)
- **`vision-hu.md`** — capability-based security tervezési alapok
- **`roadmap-hu.md`** M2.5 — Capability Registry implementáció
- **`CLI-CPU/docs/authcode-hu.md`** — AuthCode trust chain specifikáció
- **`CLI-CPU/docs/secure-element-hu.md`** — F6.5 Secure Edition (opcionális megerősítés)
- **`CLI-CPU/docs/security-hu.md`** — eliminált CWE-k

---

## Verziótörténet

| Verzió | Dátum | Változás |
|---|---|---|
| 1.0 | 2026-04-25 | Első verzió: FenySoft Strict whitelist üzleti modell, multi-tier árképzés, NEM-támogatott opciók explicit listája biztonsági indoklással, Android-modell analógia, F6.5 Secure Edition viszony |
| 1.1 | 2026-04-28 | **HMAC verify → CST HW lookup**. SipHash referenciák törölve. osreq-007 hivatkozás OBSOLETE. A trust model mechanizmusa CST-alapúra frissítve, a lényeg (Strict whitelist, single trust anchor) változatlan. |
