# Neuron OS Boot Sequence — Szoftver/Hardver Kölcsönhatás

> A Neuron OS indulási folyamata lépésről lépésre — szoftver és hardver együttműködése a bekapcsolástól az első alkalmazás actor-ig.

---

## Repo felelősségek

A boot folyamat **két repo** kódját érinti — fontos tudni, mi hol él:

| Repo | Mit tartalmaz? | Példa fájlok |
|------|---------------|--------------|
| **[FenySoft/CLI-CPU](https://github.com/FenySoft/CLI-CPU)** | A chip RTL-je (Verilog), Seal Core firmware (mask ROM), MMIO register definíciók, HW mailbox FIFO, eFuse root hash | `rtl/rich_core.v`, `rtl/seal_core.v`, `rtl/qspi_controller.v`, `rtl/mailbox_fifo.v` |
| **[FenySoft/NeuronOS](https://github.com/FenySoft/NeuronOS)** | Az OS kódja amit a bootloader a flash-ről tölt be: `Boot.Main()`, actor runtime, kernel actor-ok, device actor-ok, alkalmazások | `src/NeuronOS.Boot/`, `src/NeuronOS.Core/`, `src/NeuronOS.Devices/` |

**Szabály:** A Seal Core firmware a **hardver része** (mask ROM-ba égetve, a chip-pel szállítják). A NeuronOS a **szoftver** (flash-en, frissíthető, **BitIce aláírással védve**). Két repo, két életciklus, két licenc (CERN-OHL-S vs Apache-2.0).

---

## Kriptográfiai alapok — PQC (Post-Quantum Cryptography)

A CFPU **nem használ klasszikus kriptográfiát** (ECDSA, Ed25519, RSA) — ezek kvantumszámítógéppel törhetők (Shor-algoritmus). Helyette:

| Fogalom | Leírás |
|---------|--------|
| **BitIce** | Hash-alapú PQC PKI framework ([github.com/BitIce.io/BitIce](https://github.com/BitIce.io/BitIce)). |
| **WOTS+** | Winternitz One-Time Signature Plus — az aláírási primitív. Egyszer használható (stateful). |
| **LMS** | Levin-Merkle Signature — Merkle-fa alapú, több WOTS+ leaf-et kezel. |
| **HSS** | Hierarchical Signature Scheme — LMS fák hierarchiája, korlátlan aláírásszám. |
| **SHA-256** | Az egyetlen hash funkció — minden crypto erre épül. |
| **Neuron OS HSM Card** | Külső JavaCard smart card — a fejlesztő fizikai eszköze, WOTS+ private key-t tartalmazza. |
| **eFuse root hash** | 256 bit SHA-256, chip gyártáskor beégetve — a trust chain gyökere, **immutable**. |
| **Seal Core** | Dedikált biztonsági core — mask ROM firmware, SHA-256 + WOTS+ HW accelerátor. Ő végzi a boot verifikációt, **nem a Rich core**. |

**Miért hash-based, nem elliptic curve?**

| Szempont | ECDSA/Ed25519 | WOTS+/LMS (BitIce) |
|----------|--------------|---------------------|
| Kvantum ellenállás | ❌ Shor töri | ✅ Csak Grover (2× speedup, elfogadható) |
| HW komplexitás | Komplex EC math | **Csak SHA-256** |
| State kezelés | Stateless | Stateful (NIST SP 800-208 — HSM kötelező) |
| CFPU-n | Drága tranzisztor-költség | Olcsó (SHA-256 már amúgy is kell) |

---

## Trust Chain — a bizalom lánca

```
[Chip eFuse: 32 byte SHA-256 Root Hash]
  ← gyártáskor beégetve, IMMUTABLE, 30+ év élettartam
     │
     ▼ (verifikáció)
[BitIce CFPU Foundation Root CA]
  ← offline HSM vault-ban, FenySoft birtokában
     │
     ▼ (delegate cert)
[Neuron OS Vendor Delegate Cert]
  ← vendor-specifikus (pl. FenySoft)
     │
     ▼ (fejlesztő HSM Card-ja)
[Neuron OS HSM Card] ◄── FIZIKAI SMART CARD a fejlesztő kezében
     │
     ▼ (actor-specifikus aláírás)
[CIL Binary Cert (BitIceCertificateV1)]
  ← WOTS+ aláírás az adott bytecode-ra
     │
     ▼ (boot-kor vagy hot-load-kor)
[CFPU verifikáció] → OK: futtatás / FAIL: zeroization + halt
```

---

## BitIceCertificateV1 — az aláírás formátuma

| Mező | Méret | Leírás |
|------|-------|--------|
| Version | 1 byte | `0x01` |
| Type | 1 byte | `0x01` (Card / actor binary) |
| H | 1 byte | Merkle tree magasság (5-15, default 10 → 1024 leaf) |
| SubjectId | 16 byte | Aláírt entitás ID (actor / code identifier) |
| IssuerId | 16 byte | Kibocsátó ID (Neuron OS Vendor) |
| PkHash | 32 byte | `SHA-256(CIL bytecode)` — a cert-et a kódhoz köti |
| SignatureIndex | 4 byte | LMS leaf index (anti-replay, single-use) |
| **Compact méret** | **71 byte** | |

---

## Neuron OS HSM Card

**Külső hardver** — dedikált, egyetlen célú JavaCard smart card. Minden fejlesztőnek **kötelező** aki actor-okat deploy-ol a CFPU-ra.

**Hardveres garanciák:**

| Tulajdonság | Mechanizmus |
|-------------|-------------|
| Single-use leaf index | NVRAM counter, atomic write-before-sign |
| Kulcs nem klónozható | Private key **SOHA nem hagyja el** a kártyát |
| Rollback lehetetlen | Tamper detection + secure element zone |
| Backup nem készíthető | Private key export **DISABLED** |
| Side-channel védett | Constant-time WOTS+ implementáció |

**HSM Card APDU parancsok:**

| Parancs | Mit csinál |
|---------|-----------|
| `SELECT_APPLET` | Neuron OS Applet kiválasztása, autentikáció |
| `SIGN_CIL_HASH <SHA-256 hash>` | WOTS+ aláírás → BitIceCertificateV1 (71 byte compact) |
| `GET_FULL_CERT` | Teljes cert (2535 byte, Merkle proof-fal) |
| `GET_CERT_CHAIN` | Cert chain a root-ig |
| `GET_REMAINING` | Hátralévő felhasználható leaf-ek száma |
| `ROTATE_KEY` | Új sub-tree (HSS mélyebb szint) — ha elfogytak a leaf-ek |

**NIST SP 800-208 követelmény:** *"State management for stateful hash-based signature schemes SHALL be performed by the cryptographic module that contains the private key, and SHALL NOT rely on external software."* — Ezért **kötelező** a hardveres HSM Card, szoftveres state tracking **TILTOTT**.

---

## Áttekintés — a teljes boot szekvencia

```
[Tápfeszültség]
     │
     ▼
[1. Power-On Reset] ──── HW: POR circuit, minden core reset
     │
     ▼
[2. Seal Core indul] ──── HW: Mask ROM firmware (immutable, nem flash!)
     │                     HW: Self-test, eFuse root hash olvasás
     │                     HW: QSPI flash → SRAM másolás
     │                     HW: WOTS+/LMS aláírás verifikáció (HW accelerátor)
     │
     ├── FAIL → ZEROIZATION + HALT (minden RAM törlés, chip lockdown)
     │
     ▼ OK
[3. Rich core indul] ──── HW: Seal Core jelzi: verified code ready
     │                     HW: Rich core Quench-RAM CODE régióból indul
     │                     SW: NeuronOS CIL kód fut (VERIFIED)
     ▼
[4. Boot.Main()] ──────── SW: MMIO discovery, core count, mailbox mapping
     │                     Kód: NeuronOS repo (src/NeuronOS.Boot/)
     ▼
[5. Root Supervisor] ──── SW: Első actor, Admin capability
     │                     HW: Mailbox FIFO enable
     ▼
[6. Kernel Actors] ────── SW: Scheduler, Router, CapabilityRegistry, Supervisors
     │                     HW: Core status registers, mailbox address table
     ▼
[7. Device Actors] ────── SW: UART, GPIO, Timer, Flash device actors
     │                     HW: Periféria MMIO régiók aktiválása
     ▼
[8. Nano Core-ok Wake] ── HW: Wake interrupt, code loading Nano SRAM-ba
     │                     SW: Scheduler dönt melyik core-ra melyik actor
     ▼
[9. App Supervisor] ───── SW: Alkalmazás actor tree spawn
     │
     ▼
[10. Első alkalmazás] ─── SW: Alkalmazás actor-ok futnak Nano core-okon
     │                     HW: Inter-core mailbox messaging aktív
     ▼
[11. Rendszer üzemkész] ── Minden core dolgozik, message-ek folynak
```

---

## 1. Tápfeszültség megjelenik (Power-On Reset)

| Szoftver | Hardver |
|----------|---------|
| *Nincs még szoftver — minden HW.* | Minden CFPU core kap egy **reset jelet** (POR — Power-On Reset circuit). A reset vonal aktív amíg a tápfeszültség nem stabil. |
| | Minden core: PC = 0, SRAM tartalma **undefined**, mailbox FIFO-k üresek. |
| | A **Seal Core** (1 db, dedikált security core) indul **elsőként** — mask ROM firmware. |
| | A **Rich core** reset után **vár** — a Seal Core jelzésére indul. |
| | A **Nano core-ok** (10 000+) reset után **sleep módban** maradnak. |

**HW követelmény:**
- POR (Power-On Reset) circuit
- Seal Core mask ROM address hardwired
- Rich core **nem indul** amíg a Seal Core nem engedélyezi
- Nano core-ok default sleep state (wake-on-mailbox-interrupt)

---

## 2. Seal Core — self-test + secure boot verifikáció

> A Seal Core egy **dedikált, nem-programozható biztonsági core**. Mask ROM firmware-rel fut — nem flash-ről, nem SRAM-ból. A Rich core **soha nem lát unverified kódot**.

| Seal Core (HW) | Leírás |
|-----------------|--------|
| **2a. Self-test** | A Seal Core ellenőrzi saját integritását (ROM hash, HW accelerátor teszt). |
| **2b. eFuse root hash olvasás** | 32 byte SHA-256 hash — gyártáskor beégetve, **immutable**, 30+ év. |
| **2c. QSPI flash olvasás** | A Seal Core MMIO-n keresztül olvassa a külső flash-t (NeuronOS CIL binary + BitIce cert). |
| **2d. WOTS+/LMS verifikáció** | Dedikált HW accelerátorokkal (SHA-256 + WOTS+ verifier + Merkle path verifier). |

**A verifikáció lépései (mind HW-ben):**

```
1. SHA-256(CIL bytecode) kiszámítás           ← HW SHA-256 unit (~80 cycle/block)
2. Összehasonlítás a cert PkHash mezőjével     ← code-hash check
3. WOTS+ public key rekonstrukció              ← HW WOTS+ verifier (~500 SHA-256 op)
4. Leaf hash → Merkle root kiszámítás          ← HW Merkle path verifier (h=10 → 10 hash)
5. Összehasonlítás az eFuse root hash-sel      ← root-of-trust check
```

**Teljes verifikáció:** ~512 SHA-256 művelet = ~41 000 ciklus @ 1 GHz = **~41 µs**

| Eredmény | Mi történik |
|----------|-------------|
| **VALID** | Seal Core a verified kódot a **Quench-RAM CODE régióba** írja, SEAL-eli (immutable), majd jelzi a Rich core-nak: **indulhat**. |
| **INVALID** | **ZEROIZATION** — minden RAM és cache törlődik. Chip **lockdown** módba kerül. Semmilyen kód nem fut. LED villog / UART hiba (ha van). |

**Seal Core HW accelerátorok:**

| Egység | Gate count | Funkció |
|--------|-----------|---------|
| SHA-256 HW unit | ~5K gate | ~80 cycle/block (512-bit input) |
| WOTS+ verifier | ~3K gate | SHA-256 chain rekonstrukció (67 chain × ~7.5 hash) |
| Merkle path verifier | ~2K gate | h=10 iteráció = 10 SHA-256 hash |
| **Összesen** | **~10K gate** | Teljes BitIce cert verify ~41 µs |

**Seal Core vs Rich Core vs Nano Core:**

| Tulajdonság | Seal Core | Rich Core | Nano Core |
|-------------|-----------|-----------|-----------|
| Firmware | **Mask ROM** (immutable) | Flash-ről töltött CIL | Flash-ről töltött CIL (T0 subset) |
| Programozható? | **NEM** (HW-burned) | Igen | Igen |
| SRAM | 64 KB (trusted zone) | 64-256 KB | 4-16 KB |
| SHA-256 + WOTS+ HW | **Igen** (dedikált) | Nem | Nem |
| CIL futtatás | Belső firmware only | Teljes CIL | CIL-T0 subset |
| Darabszám / chip | 1 (vagy 2, redundancia) | 1-4 | 10 000+ |
| Boot sorrend | **ELSŐ** | Második (Seal Core után) | Utolsó |

**Miért nem a Rich core végzi a verifikációt?**
- A Rich core **programozható** — ha a verifikáció a Rich core-on futna, egy támadó módosíthatná a verifikációs kódot a flash-en
- A Seal Core **mask ROM** — a firmware fizikailag nem módosítható, a chip élettartama alatt
- A Seal Core a **trust boundary** — ő az egyetlen, aki az eFuse root hash-hez hozzáfér és a Quench-RAM CODE régiót SEAL-eli

**HW követelmény:**
- Seal Core mask ROM (firmware HW-burned, immutable)
- eFuse root hash register (R/O, `0xF0002000`, 32 byte)
- QSPI flash controller (MMIO: `0xF0001000-0xF000100F`)
- SHA-256 + WOTS+ + Merkle HW accelerátorok (~10K gate)
- Quench-RAM CODE régió + SEAL mechanizmus
- Rich core start signal (Seal Core → Rich core: "verified, go")

---

## 3. Rich core indul — verified CIL kód

| Szoftver | Hardver |
|----------|---------|
| A Rich core a **Quench-RAM CODE régióból** indul — ez a Seal Core által SEAL-elt, verified NeuronOS binary. | A Seal Core jelzésére a Rich core **reset elengedődik**. |
| A CIL kód **már verified** — a Rich core nem végez további ellenőrzést. | A Quench-RAM CODE régió **read-only** (SEAL-elt) — a Rich core nem tudja módosítani. |
| Az első CIL metódus: `NeuronOS.Boot.Main()` | A Rich core CIL execution engine aktiválódik: eval stack, locals, call frames. |

**HW követelmény:**
- Quench-RAM CODE régió (R/O a Rich core számára, SEAL-elt a Seal Core által)
- Rich core start signal fogadás
- CIL execution engine (microcode)

---

## 4. NeuronOS.Boot.Main() — rendszer inicializálás

| Szoftver | Hardver |
|----------|---------|
| A Rich core futtatja `NeuronOS.Boot.Main()`-t. | A Rich core folyamatosan CIL-t hajt végre. |
| **Innentől a NeuronOS repo kódja fut.** | A Seal Core verifikálta — a kód hiteles. |
| | |
| **3a. GC inicializálás** | A Rich core SRAM heap régiójában **bump allocator** indul. |
| `THeapManager.Init(heapStart, heapEnd)` | Mark-sweep GC később, amikor a heap megtelik. |
| | |
| **3b. Core discovery** | A Rich core olvassa a **core count register-t** (MMIO `0xF0000100`). |
| `var ANanoCoreCount = Mmio.Read<int>(0xF0000100)` | Visszaadja: pl. 10 000 (Nano) + 1 (Rich) = 10 001. |
| `var ARichCoreCount = Mmio.Read<int>(0xF0000104)` | Két külön register: Nano count és Rich count. |
| | |
| **3c. Mailbox mapping** | A Rich core olvassa a **mailbox base address register-t** (MMIO `0xF0000200`). |
| Minden core mailbox FIFO-jának fizikai címét kiszámolja: | Mailbox address = base + core_id × FIFO_SIZE. |
| `base + core_id * FIFO_SLOT_SIZE` | Pl. FIFO_SLOT_SIZE = 64 byte (8 slot × 8 byte/slot). |
| | |
| **3d. Interrupt vektor beállítás** | A Rich core interrupt controller-e MMIO-n konfigurálható (MMIO `0xF0000600`). |
| Beállítja melyik interrupt → melyik handler: | Interrupt sources: mailbox not-empty, watchdog, trap from Nano core. |
| `Mmio.Write(0xF0000600, MAILBOX_IRQ_HANDLER_ADDR)` | |

**Forráskód helye:** `FenySoft/NeuronOS` repo — `src/NeuronOS.Boot/TBoot.cs`

**HW követelmény:**
- Core count registers (R/O MMIO: `0xF0000100`, `0xF0000104`)
- Mailbox base address register (R/O MMIO: `0xF0000200`)
- Interrupt controller (R/W MMIO: `0xF0000600`)
- SRAM heap region (a GC bump allocator számára)

---

## 5. Root Supervisor actor elindul

| Szoftver | Hardver |
|----------|---------|
| `Boot.Main()` létrehozza az **első actor-t**: `TRootSupervisor`. | A Rich core saját SRAM-jában jön létre az actor state. |
| | Nincs core váltás — a root supervisor a Rich core-on fut. |
| `TRootSupervisor.Init()` visszaad: | |
| - üres child lista | |
| - restart stratégia = "mindig újraindít" | |
| - max restart count = végtelen | |
| | |
| A Rich core **saját mailbox FIFO-ja aktiválódik**: | MMIO write: `Mmio.Write(0xF0000300 + rich_core_id * 4, MAILBOX_ENABLE)` |
| Innentől a root supervisor üzeneteket fogadhat. | A HW mailbox FIFO aktív — bejövő üzenetek interrupt-ot generálnak. |
| | |
| A root supervisor **TActorRef capability-t kap**: | A capability signing **WOTS+/LMS** alapú: |
| `[core-id: Rich][offset: 0][perms: Admin]` | A `TCapabilityRegistry` (5d lépés) fogja kezelni a runtime capability-ket. |
| + BitIce aláírás a boot key-jel | Boot-kor a root supervisor speciális: a bootloader trust-ből származik. |
| | |
| Ez az **egyetlen** actor akinek Admin capability-je van. | |
| Minden más actor **ettől kap jogot** (capability delegation). | |

**Forráskód helye:** `FenySoft/NeuronOS` repo — `src/NeuronOS.Core/TRootSupervisor.cs` (M2.1 milestone)

**Mi az Admin capability?** A root supervisor korlátlan jogokkal rendelkezik:
- Spawn bármilyen actor-t
- Kill bármilyen actor-t
- Delegálhat bármilyen permission-t
- Hozzáfér minden device actor-hoz

**Miért csak egy Admin van?** Biztonsági okokból — a capability model lényege, hogy a jogok delegálással terjednek, nem globális hozzáféréssel. A root supervisor az egyetlen "trust anchor" a runtime-ban (az eFuse root hash a HW trust anchor).

**HW követelmény:**
- Mailbox FIFO enable per core (R/W MMIO: `0xF0000300 + core_id * 4`)

---

## 6. Kernel aktorok spawn-olódnak

| Szoftver | Hardver |
|----------|---------|
| A root supervisor spawn-olja a **kernel actor-okat** sorban: | |
| | |
| **5a.** `TKernelCoreSupervisor` | Még mindig a Rich core-on fut minden. |
| Felügyeli a kernel actor-okat (scheduler, router, registry). | A Nano core-ok **még alszanak**. |
| OneForOne stratégia — ha egy kernel actor crash-el, csak azt indítja újra. | |
| | |
| **5b.** `TScheduler` | A scheduler olvassa a **core status register-eket**: |
| Actor-to-core hozzárendelés, load balancing. | `Mmio.Read(0xF0000400 + core_id * 4)` → Sleeping / Running / Error |
| Nyilvántartja: melyik core szabad, melyik mit futtat. | Egyelőre minden Nano core "Sleeping" státuszban van. |
| | |
| **5c.** `TRouter` | A router olvassa a **mailbox address table-t**: |
| Logikai ref → fizikai cím feloldás. | `Mmio.Read(0xF0000800 + core_id * 4)` → mailbox FIFO fizikai cím |
| Cache-eli a mapping-eket a gyors lookup-hoz. | Ez a HW → SW "telefonkönyv": core-id → mailbox FIFO cím. |
| | |
| **5d.** `TCapabilityRegistry` | Runtime capability kezelés **WOTS+/LMS** alapú aláírásokkal. |
| Capability-k kiadása, delegálás, visszavonás. | A boot cert chain-ből származó trust-öt delegálja tovább. |
| Nyilvántartja az összes kiadott capability-t. | F6+: HW SHA-256 accelerator gyorsítja a hash compute-ot. |
| Visszavonáskor broadcast: érintett router-ek frissítik a cache-t. | |
| | |
| **5e.** `TKernelIoSupervisor` | Még **nem** spawn-ol device actor-okat. |
| A device actor-ok felügyelete (UART, GPIO, Timer, Flash). | Ez a következő lépés (6.). |

**Spawn sorrend miért fontos?**
1. A `TKernelCoreSupervisor` kell először — ő felügyeli a többit
2. A `TScheduler` kell a `TRouter` előtt — a router-nek tudnia kell melyik core szabad
3. A `TCapabilityRegistry` kell a device actor-ok előtt — azoknak capability kell
4. A `TKernelIoSupervisor` utolsó — ő spawn-olja a device actor-okat a 6. lépésben

**Forráskód helye:** `FenySoft/NeuronOS` repo — `src/NeuronOS.Core/` (M0.3 supervision + M2.1-M2.5 kernel actors)

**Állapot a 6. lépés után:**
- Rich core-on fut **7 kernel actor**: root + 2 supervisor + scheduler + router + registry + io_sup
- A Seal Core **heartbeat-et küld** a health monitor-nak (redundancia)
- A Rich core **time-sliced**: a kernel actor-ok cooperative scheduling-gel osztoznak az egyetlen core-on
- A Nano core-ok **még alszanak** — a következő lépésben kelnek fel
- Az actor tree:

```
TRootSupervisor [Admin]
├── TKernelCoreSupervisor
│   ├── TScheduler
│   ├── TRouter
│   └── TCapabilityRegistry
└── TKernelIoSupervisor
    └── (üres — device actor-ok a 6. lépésben)
```

**HW követelmény:**
- Core status registers (R/O MMIO: `0xF0000400 + core_id * 4`) — per core: Sleeping / Running / Error / Reset
- Mailbox address table (R/O MMIO: `0xF0000800 + core_id * 4`) — core-id → mailbox FIFO fizikai cím

---

## 7. Device aktorok spawn-olódnak

*(következő szakasz — kérdés esetén folytatjuk)*

---

## HW Register Összesítés

| Register / MMIO | Cím (példa) | Típus | Méret | Leírás |
|----------------|-------------|-------|-------|--------|
| Boot ROM entry | `0x00000000` | R/O | — | Rich core boot address (hardwired) |
| QSPI controller base | `0xF0001000` | R/W | 16 byte | Flash controller: config, address, size, data |
| ├─ QSPI config | `0xF0001000` | R/W | 4 byte | Enable, SPI mode, clock divider |
| ├─ QSPI flash addr | `0xF0001004` | R/W | 4 byte | Flash olvasási cím |
| ├─ QSPI binary size | `0xF0001008` | R/O | 4 byte | Binary méret (flash header-ből) |
| └─ QSPI data | `0xF000100C` | R/O | 1 byte | Következő byte a flash-ről |
| Core count (Nano) | `0xF0000100` | R/O | 4 byte | Nano core-ok száma |
| Core count (Rich) | `0xF0000104` | R/O | 4 byte | Rich core-ok száma |
| Mailbox base address | `0xF0000200` | R/O | 4 byte | Mailbox FIFO-k fizikai címtartomány kezdete |
| Mailbox enable (per core) | `0xF0000300 + core_id × 4` | R/W | 4 byte | Mailbox FIFO aktiválás bit |
| Core status (per core) | `0xF0000400 + core_id × 4` | R/O | 4 byte | Sleeping / Running / Error / Reset |
| Interrupt controller | `0xF0000600` | R/W | 16 byte | Interrupt vektor beállítás |
| Mailbox address table | `0xF0000800 + core_id × 4` | R/O | 4 byte | core-id → mailbox FIFO fizikai cím |
| **eFuse root hash** | **`0xF0002000`** | **R/O** | **32 byte** | **BitIce Foundation Root CA SHA-256 hash — gyártáskor beégetve, IMMUTABLE** |
| Seal Core status | `0xF0002020` | R/O | 4 byte | Self-test result, verify result, heartbeat |
| Seal Core → Rich core signal | `0xF0002024` | R/O | 4 byte | 0 = wait, 1 = verified + go, 2 = fail + halt |
| Quench-RAM CODE base | `0xF0002030` | R/O | 4 byte | Verified CIL binary kezdőcíme |
| Quench-RAM CODE size | `0xF0002034` | R/O | 4 byte | Verified CIL binary mérete |

---

## SRAM Layout (Rich core, az 5. lépés után)

```
┌─────────────────────────────────┐ 0x00000000
│ Boot ROM (CIL bootloader)       │ ~1 KB (R/O, chip-be égetve)
│  └─ BitIce verifier kód         │  (WOTS+/LMS + SHA-256)
├─────────────────────────────────┤ 0x00000400
│ CIL Code (flash-ről másolva)    │ ~16-48 KB (NeuronOS binary, VERIFIED)
├─────────────────────────────────┤ 0x0000C000
│ Eval Stack                      │ ~2-4 KB (CIL execution stack)
├─────────────────────────────────┤ 0x0000D000
│ Call Frames + Local Variables   │ ~4-8 KB (metódus hívások)
├─────────────────────────────────┤ 0x0000F000
│ Heap (bump allocator + GC)      │ ~16-64 KB
│  ├─ TRootSupervisor state       │  (actor állapotok)
│  ├─ TKernelCoreSupervisor state │
│  ├─ TScheduler state + core map │  (core status cache)
│  ├─ TRouter state + ref cache   │  (ref → fizikai cím mapping)
│  ├─ TCapabilityRegistry state   │  (capability lista, cert chain cache)
│  └─ TKernelIoSupervisor state   │
├─────────────────────────────────┤ 0x0002F000
│ Mailbox FIFO (Rich core saját)  │ ~64-512 byte (8-64 slot)
├─────────────────────────────────┤ 0x0002F200
│ Szabad                          │ (további actor-ok, device state-ek)
└─────────────────────────────────┘ 0x0003FFFF (256 KB)
```

---

## Core típusok összehasonlítása

| Tulajdonság | Seal Core | Rich Core | Nano Core |
|-------------|-----------|-----------|-----------|
| **Szerep** | Security / boot verify | Kernel + app runtime | Egyszerű actor futtatás |
| **Firmware** | Mask ROM (immutable) | Flash-ről (verified) | Flash-ről (verified, T0) |
| **Programozható?** | **NEM** | Igen | Igen |
| **SRAM méret** | 64 KB (trusted zone) | 64-256 KB | 4-16 KB |
| **Heap / GC** | Nincs | Van (bump + mark-sweep) | **Nincs** |
| **CIL opcode-ok** | Belső firmware only | Teljes CIL (ECMA-335) | T0 subset (integer, branch, mailbox) |
| **SHA-256 + WOTS+ HW** | **Igen** (dedikált) | Nem | Nem |
| **Exception handling** | N/A | Natív try/catch | Trap → Rich core |
| **Floating point** | N/A | Van | Nincs |
| **Mailbox FIFO** | Nincs (nem actor) | Van (HW) | Van (HW) |
| **Tipikus funkció** | Boot verify, AuthCode, heartbeat | Kernel actors, supervisor tree | 1 actor (pl. LIF neuron) |
| **Energiafogyasztás** | Alacsony (~µW) | Magasabb (~mW) | Nagyon alacsony (~µW) |
| **Darabszám / chip** | 1 (vagy 2, redundancia) | 1-4 | 10 000+ |
| **Boot sorrend** | **ELSŐ** | Második | Utolsó |

---

## Runtime Actor Loading — WOTS+/LMS verifikáció (hot code)

A boot-on kívül **runtime-ban is tölthetők** új actor-ok (hot code loading). A `THotCodeLoader` kernel actor végzi a verifikációt:

```
Developer:
  1. C# kód → CIL bytecode (dotnet build)
  2. SHA-256(bytecode) kiszámítás
  3. Neuron OS HSM Card-ra küldés: SIGN_CIL_HASH <hash>

HSM Card:
  4. NVRAM leaf index counter olvasás
  5. Atomic: counter increment + WOTS+ aláírás
  6. BitIceCertificateV1 visszaadás (71 byte compact)

Developer:
  7. Csomag: [bytecode + certificate]
  8. Deploy a CFPU-ra (hálózat / flash / UART)

CFPU (THotCodeLoader actor):
  9.  SHA-256(bytecode) == cert.PkHash?        → code-hash check
  10. BitIce cert-chain verify → eFuse root?    → chain check
  11. Revocation check (cert visszavonva?)       → revocation check
  12. Mind OK → actor spawn                     → VAGY: BLOCK, nem tölt be
```

---

## Trust Anchors Összefoglalás

| Anchor | Típus | Hol van? | Módosítható? | Élettartam |
|--------|-------|----------|--------------|------------|
| eFuse Root Hash | 256-bit SHA-256 | Chip eFuse | **HW-immutable** | 30+ év |
| Boot ROM Code | CIL bytecode | Chip ROM | **HW-immutable** | Chip élettartam |
| Foundation Root CA | Public key hash | eFuse (tranzitívan) | Immutable | Deployment élettartam |
| Vendor Delegate Cert | Delegate cert | HSM vault (offline) | Kriptográfiailag aláírt | Key rotation-ig |
| Developer HSM Card | Hardver token | Fizikai eszköz | Tamper-resistant | Kártya élettartam |
| WOTS+ Leaf Index | State counter | HSM Card NVRAM | Atomic write-before-sign | Per deployment |

---

## Milyen sorrendben épül ez a .NET reference implementációban?

A boot sequence fenti leírása a **CFPU hardverre** vonatkozik. A .NET reference runtime (`src/NeuronOS.Core/`) **szimulálva** futtatja ugyanezt:

| Boot lépés | CFPU HW-en | .NET Reference Impl-ben |
|-----------|------------|------------------------|
| 1. POR | HW reset | `new TActorSystem()` |
| 2. Seal Core verify | Mask ROM → QSPI → WOTS+/LMS verify → Quench-RAM SEAL | Nincs (a .NET CLR tölti be a kódot) |
| 3. Rich core indul | Seal Core signal → verified CIL futtatás | Nincs (implicit) |
| 4. Boot.Main() | MMIO register olvasás | Constructor: core count = `Environment.ProcessorCount` |
| 5. Root Supervisor | Első actor a Rich core-on | `system.Spawn<TRootSupervisor>()` |
| 6. Kernel actors | Rich core time-sliced | `system.Spawn<TScheduler>()`, stb. |
| 7. Device actors | MMIO periféria actor-ok | Mock device actor-ok (szimulált UART, GPIO) |
| 8. Nano wake | HW wake interrupt | `Task.Run()` — thread per "core" |
| 9-11. App actors | Nano core-okon futnak | Thread pool-on futnak |

**Ez a reference impl célja:** igazolni, hogy az actor modell működik, a supervision helyes, a message routing korrekt — **mielőtt szilícium lenne**.
