# OSREQ-001: Fa topológiájú interconnect a core-ok között

> English version: [osreq-001-tree-interconnect-en.md](osreq-001-tree-interconnect-en.md)

> Version: 1.2

> **Állapot:** Resolved — lásd a lenti banner
>
> **Érintett CFPU fázis:** F4 (multi-core FPGA), F5 (heterogén), F6 (silicon)

---

> ## ✅ RESOLVED
>
> A CLI-CPU csapat ezt a topológia-igényt **eldöntötte** — de nem a lentebb javasolt tiszta fa (fat tree) topológiával, hanem egy **4-szintű hierarchikus mesh + crossbar** architektúrával, amely ugyanazt a mögöttes igényt (lokalitás-tudatos, hierarchikus, a supervisor tree-hez illeszkedő, O(1) routing-döntésű interconnect) elégíti ki. Kanonikus, hatályos specifikáció: [CLI-CPU/docs/interconnect-hu.md](https://github.com/FenySoft/CLI-CPU/blob/main/docs/interconnect-hu.md) v3.8.
>
> **A tiszta fat tree-t a CLI-CPU explicit kizárta** ("Kizárt alternatívák" táblázat): *"Bottleneck és SPOF a gyökéren; horizontális linkekkel konvergál a hierarchikus mesh-hez."* — vagyis egy fat tree horizontális (redundáns) linkekkel kiegészítve pont a lent leírt hierarchikus mesh+crossbar konstrukcióhoz konvergál.
>
> **Elfogadott topológia:** L0 Cluster (4×4 mesh, XY dimension-order routing, wormhole switching, 16 core) → L1 Tile (8×8 crossbar) → L2 Régió (8×8 crossbar) → L3 Chip (csillag topológia, Seal Core a geometriai középen). Az L1–L3 crossbar-ok mindegyike **mindig 1 determinisztikus hop** a saját szintjén, Virtual Cut-Through (VCT) switching-gel. Arbitráció: iSLIP (1 ciklus/döntés). Flow control: credit-based (4 credit/csatorna) + Virtual Output Queueing (VOQ, minden inputon minden kimenő port felé külön sor). Forgalom-izoláció: 2 Virtual Network (VN0 = control/supervisor, VN1 = actor). Deadlock-mentesség: XY routing (aciklikus csatorna-rendezés, Dally & Seitz 1987) + VCT + VOQ + credit flow control.
>
> **A lenti "Routing hierarchia" és hop/latencia becslések (Level 0–3, ~2-4/~6-10/~4-8/~50-200 ciklus) elavultak** — egy 2-szintű (cluster/inter-cluster) modellt feltételeztek; a tényleges architektúra 4 szintű (cluster/tile/régió/chip), eltérő hop-struktúrával. A kanonikus hop-számot és ciklus-latenciát lásd `interconnect-hu.md` "Hop-szám és latencia összefoglaló" szekció: szomszéd core 1 hop (~5-10 ciklus), cross-cluster azonos tile 13 hop (~39-59 ciklus), cross-tile azonos régió 15 hop (~63-109 ciklus), cross-régió 18 hop (~93-171 ciklus). Az inter-chip (multi-chip fabric) latencia F6+ témakör — jelenleg a CLI-CPU oldalon sincs lezárva, ez továbbra is nyitott kérdés.
>
> A dokumentum alább **eredeti, Draft állapotában** marad archívumként.

---

## Összefoglaló

A Symphact supervisor tree, a capability delegation modell és a memóriakezelés **fa szerkezetű hierarchiát** feltételez. Az interconnect topológiának ezt a fát kell tükröznie — nem flat bus, nem uniform mesh, hanem **hierarchikus fa (fat tree)**.

## Kontextus — miért fa?

### 1. A szoftver természetesen fa

A Symphact alapstruktúrája — az actor supervisor tree — fa:

```
TRootSupervisor [Admin]              ← Rich Core
├── TKernelCoreSupervisor
│   ├── TScheduler
│   ├── TRouter
│   └── TCapabilityRegistry
└── TKernelIoSupervisor
    └── Device actors...
        └── App actors...
            └── Nano Core actors...
```

A capability delegation is fa: a root supervisor az egyetlen Admin, és **delegálással** terjednek a jogok lefelé. A hibakezelés (supervision) is fa: a hibás actor-t a **szülő** kezeli.

### 2. A memória fizikailag fa

A CFPU-n nincs shared memory. Minden core saját, privát SRAM-mal rendelkezik:

```
                         Chip
                          │
                   ┌──────┴──────┐
              Seal Core     Rich Core(s)        Level 0: privileged
                              │
               ┌──────────────┼──────────────┐
           Cluster 0      Cluster 1      Cluster K    Level 1: Nano cluster-ek
            /  |  \        /  |  \        /  |  \
          N0  N1  N2     N3  N4  N5     N6  N7  N8   Level 2: Nano core-ok
          │   │   │      │   │   │      │   │   │
         4KB 4KB 4KB    4KB 4KB 4KB    4KB 4KB 4KB   Level 3: privát SRAM
```

Az „adatmegosztás" kizárólag mailbox üzenetekkel lehetséges, és az üzenet útvonala **a fában felfelé-lefelé** halad.

### 3. Az üzenet útja

Ha `N2` (Cluster 0) küld üzenetet `N5`-nek (Cluster 1):

```
N2 [saját SRAM]
  → N2 outbox FIFO (HW)
    → Cluster 0 router (felfelé)               1 hop
      → Chip-szintű router (Rich Core szint)    2 hop
        → Cluster 1 router (lefelé)             3 hop
          → N5 inbox FIFO (HW)                  4 hop
            → N5 [saját SRAM-ba másolódik]
```

Ugyanazon cluster-en belül (N0 → N2): **1 hop** (cluster router-en belül marad).

### 4. Multi-chip: a fa tovább nő

```
              Fabric (multi-chip)
             /         |         \
         Chip 0     Chip 1     Chip 2       inter-chip link
           │          │          │
        Rich Core  Rich Core  Rich Core
        /  |  \    /  |  \    /  |  \
       Cl0 Cl1..  Cl0 Cl1..  Cl0 Cl1..
```

A fa címzést a HW routing cím (`[chip-id][cluster-id][core-offset]`) kódolja, amelyet a router a hierarchiában felfelé-lefelé haladva old fel. A `TActorRef` maga chip-local, opaque 32-bit CST index marad (lásd `actor-ref-scaling-hu.md` §1.c) — chip-határon túli címzés proxy-aktor mintával történik (§3.c), nem beágyazott chip-id mezővel.

## Jelenlegi szoftveres megoldás

A .NET referencia implementációban (`TActorSystem`) nincs topológia-tudatosság — minden actor egy flat dictionary-ben él, és a `Send()` közvetlen lookup. Ez a CFPU-ra nem skálázható.

## Javasolt hardveres viselkedés

### Cluster fogalom bevezetése

| Elem | Leírás |
|------|--------|
| **Cluster** | 4-16 Nano core fizikai csoportja, saját lokális router-rel |
| **Cluster router** | Cluster-en belüli üzenetek közvetlen továbbítása (1 hop, ~2-4 ciklus) |
| **Chip router** | Cluster-ek közötti üzenetek továbbítása (2+ hop) |
| **Inter-chip bridge** | Chipek közötti üzenetek (SPI/LVDS link) |

### Routing hierarchia

```
Level 0: Intra-cluster    N→N azonos cluster-ben     1 hop    ~2-4 ciklus
Level 1: Inter-cluster    N→N más cluster-ben         2 hop    ~6-10 ciklus
Level 2: N→Rich / Rich→N  Supervisor kommunikáció     1-2 hop  ~4-8 ciklus
Level 3: Inter-chip        Chip→Chip                  3+ hop   ~50-200 ciklus
```

### MMIO regiszterek (javaslat)

| Cím | Név | R/W | Leírás |
|-----|-----|-----|--------|
| `0xF0000100` | `NANO_CORE_COUNT` | R/O | Nano core-ok teljes száma |
| `0xF0000104` | `RICH_CORE_COUNT` | R/O | Rich core-ok száma |
| `0xF0000108` | `CLUSTER_COUNT` | R/O | Cluster-ek száma |
| `0xF000010C` | `CORES_PER_CLUSTER` | R/O | Core-ok száma cluster-enként |
| `0xF0000110` | `CLUSTER_ID` | R/O | Az adott core melyik cluster-ben van |
| `0xF0000114` | `CHIP_ID` | R/O | Multi-chip: melyik chip |

### Routing tábla

A chip router-nek **nem kell flat routing táblát** tartania minden core-hoz. Ehelyett hierarchikus címzés:

```
Cél cím: [chip-id].[cluster-id].[core-offset]

Routing döntés:
  if chip-id ≠ saját → inter-chip bridge
  if cluster-id ≠ saját → chip router → cél cluster router
  if cluster-id = saját → cluster router (lokális delivery)
```

Ez O(1) routing döntés, nem O(N) lookup.

## Miért fa és miért nem mesh?

| Topológia | Előny | Hátrány CFPU kontextusban |
|-----------|-------|--------------------------|
| **Flat bus** | Egyszerű (F4 szint) | O(N) contention — 16+ core-nál használhatatlan |
| **Crossbar** | O(1) latencia | O(N²) vezetékszám — 10k core-nál fizikailag lehetetlen |
| **2D Mesh** | Jó GPU-khoz (uniform workload) | Nem illeszkedik a supervisor hierarchy-hez; uniform routing pazarló, ha a kommunikáció lokális |
| **Fa (fat tree)** | O(log N) vezeték; természetes hierarchia | Gyökér bottleneck — de fat tree csökkenti, és a supervisor tree amúgy is fa |

A **fa topológia** azért természetes a CFPU-hoz, mert:

1. **A szoftver is fa** — supervisor tree = fizikai routing tree → a logikai és fizikai hierarchia egymásra illeszkedik
2. **A kommunikáció lokális** — a legtöbb üzenet szomszédos core-ok vagy azonos cluster tagjai között megy (pl. SNN szomszédsági spike-ok)
3. **A capability delegation fa** — a routing fa tükrözi a jogosultsági hierarchiát
4. **Skálázható** — cluster hozzáadása O(1) routing bővítés, nem O(N) tábla-újraépítés

## Hatás a Symphact-re

| OS komponens | Változás |
|-------------|---------|
| `TRouter` | Routing tábla fa-alapúvá válik (hierarchikus cím lookup, nem flat hash map) |
| `TScheduler` | Actor placement figyelembe veszi a fa-lokalitást (sokat kommunikáló actor-ok azonos cluster-be) |
| `TMemoryManager` | Per-core SRAM budget + cluster-szintű aggregáció |
| `TActorRef` | Chip-local marad — 32-bit opaque CST index (`actor-ref-scaling-hu.md` §1.c); chip-határon túli címzés proxy-aktor mintával (§3.c), nem a ref bővítésével |
| Zero-copy | Csak azonos core-on belül — inter-core mindig mailbox copy |

## Nyitott kérdések (HW visszajelzés szükséges)

1. **Cluster méret** — hány Nano core / cluster az optimális? (4? 8? 16?) Trade-off: nagyobb cluster = több lokális kommunikáció, de drágább cluster router
2. **Fat tree szélesség** — a gyökér (chip router) felé milyen széles legyen a busz? Hány párhuzamos üzenet haladhat?
3. **Rich Core pozíciója** — a Rich Core a fa gyökerénél van (minden üzenet átmegy rajta?) vagy mellette (dedikált uplink)?
4. **Inter-chip link** — SPI? LVDS? Hány pin? Max message méret?
5. **Cluster router vs. hardwired** — a cluster router programozható (mikrokóddal konfigurálható routing) vagy fix logikájú?
6. **Broadcast/multicast** — supervisor restart esetén a parent-nek broadcast-olnia kell a cluster-nek. Van-e HW multicast support?

## Kereszthivatkozások

- **CLI-CPU interconnect (kanonikus, hatályos):** [`interconnect-hu.md`](https://github.com/FenySoft/CLI-CPU/blob/main/docs/interconnect-hu.md) v3.8 — a 4-szintű hierarchikus mesh + crossbar topológia, routing, switching és flow control teljes specifikációja
- CLI-CPU issue: #TODO (osreq-from-os címkével)
- Symphact roadmap: M2.3 (Router), M2.4 (Memory Manager)
- Boot sequence: `docs/boot-sequence-hu.md` — 8. lépés (Nano Core-ok Wake)
- CLI-CPU architecture: `docs/architecture-hu.md` — „Skálázódás F6-ra" szekció


---

## Changelog

| Verzió | Dátum | Összefoglaló |
|--------|-------|-------------|
| 1.2 | 2026-07-19 | **Állapot Draft → Resolved.** A CLI-CPU a tiszta fat tree topológia-igényt elvetette (gyökér bottleneck + SPOF), helyette 4-szintű hierarchikus mesh + crossbar architektúrát fogadott el (`interconnect-hu.md` v3.8) — ez elégíti ki a mögöttes igényt (lokalitás-tudatos, hierarchikus, supervisor-tree-hez illeszkedő, O(1) routing). Banner hozzáadva a dokumentum tetejére, a törzs változatlanul archívumként marad. Kereszthivatkozás hozzáadva |
| 1.1 | 2026-07-19 | `TActorRef` elavult HMAC-alapú formátum-hivatkozás javítva CST-modellre (§4 Multi-chip szekció + „Hatás a Symphact-re" tábla), az `actor-ref-scaling-hu.md` v3.0 hatályos döntésével összhangban |
| 1.0 | 2026-04-20 | Kezdeti kiadás |
