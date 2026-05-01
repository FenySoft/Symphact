# NLnet NGI Zero Commons Fund — Pályázati vázlat (Symphact)

> **Deadline:** 2026-06-01, 12:00 CEST
> **Form:** https://nlnet.nl/propose/
> **Kiírás:** NGI Zero Commons Fund — 13. nyílt kör (ugyanaz a kör, mint a párhuzamos CLI-CPU beadás 2026-04-14-én)
> **Státusz:** DRAFT — beadás-előkészítés a 2026-06-01-i határidőre
> **Megjegyzés:** A pályázat **angolul kerül beadásra** (NLnet követelmény). Ez a magyar változat tájékoztató jellegű. A beadandó angol verzió: `nlnet-application-draft-en.md`.

> English version: [nlnet-application-draft-en.md](nlnet-application-draft-en.md)

> Version: 1.1

---

## Pályázati kiírás

NGI Zero Commons Fund

## Pályázat neve

**Symphact: Capability-alapú aktor runtime biztonságos .NET számításhoz**

## Weboldal / Wiki

https://github.com/FenySoft/Symphact

---

## Kivonat

A Symphact **capability-alapú aktor runtime .NET-re**, arra az elvre építve, hogy *minden állapottal rendelkező entitás egy aktor, és a kommunikáció kizárólag immutábilis üzeneteken keresztül történik*. A projekt befejezte az M0.1–M0.4 mérföldköveket: **142 zöld xUnit teszt** fedi le a core primitíveket — `TMailbox` (FIFO mailbox), `TActorRef` (capability token), `TActor<TState>` (absztrakt aktor), `TActorContext` (handler context), `TActorSystem` (runtime), supervision (M0.3 — `ISupervisorStrategy`, OneForOne / AllForOne, lifecycle hookok, aktor hierarchia), és a per-aktor parallelizmusú scheduler API (M0.4 — `IScheduler`, `TInlineScheduler`, `TDedicatedThreadScheduler`, CFPU „1 core = 1 actor" szimuláció). Mindezt szigorú TDD módszertannal, .NET 10-en, Apache-2.0 licensszel. **Az M0.3 supervision és M0.4 scheduler a v0.1 (2026 április) és a beadás között elkészült, és kifejezetten kívül esik e pályázat scope-ján.** **Ez a pályázat a működő aktor magról egy használható, biztonságos aktor operációs rendszerre való átmenetet finanszírozza**: persistencia, location transparency, capability-alapú biztonság delegálással/visszavonással, és az első device aktorok.

**Miért aktor OS, és miért most?**

A jelenlegi operációs rendszerek (Linux, Windows, macOS) az 1970-es években hozott architekturális döntéseket viselnek — shared memory, monolitikus kernel, POSIX jogosultságok, fork/exec. Ezek a megoldások egyre kevésbé illenek a modern valósághoz: 1000+ core chip-enként, AI-vezérelt fenyegetések, supply chain támadások (log4j, xz-utils), és hibatűrés iránti igény safety-critical rendszerekben. Joe Armstrong (az Erlang megalkotója) erről beszélt 2014-es "The Mess We're In" előadásában — aktor-orientált hardver kellett volna, de akkor nem létezett. **Mostanra létezik.** A nyílt szilícium (Tiny Tapeout, eFabless, IHP MPW), az érett aktor framework-ök (Akka.NET, Orleans, Akka JVM), és a Dennard skálázás végének összhatása életképessé teszi egy tiszta lapról indított aktor OS-t 2026-ban.

A Symphact négy bizonyított ötletet kombinál egy runtime-ban, modern alapokon:

- **Aktor modell** (Erlang/OTP, 40+ év telco/pénzügy) — let-it-crash + supervision
- **Capability-alapú biztonság** (seL4, CHERI) — hamisíthatatlan referenciák, nincs globális namespace
- **Típus-biztos runtime** (Singularity MS Research prototípus, 2003) — memória-biztonság ISA szinten
- **Determinisztikus üzenetküldés** (QNX kereskedelmi demonstráció) — formális verifikálhatóság

**A pályázat három konkrét eredményt céloz (a már leszállított M0.1–M0.4 alapra építve):**

1. **Persistencia + Location transparency (M1–M2):** Event-sourcing journal (`IPersistenceProvider`, `TInMemoryJournal`, `TSqliteJournal`), snapshot + replay az M0.3 supervision lifecycle-jával integrálva, így az aktor állapot túléli a restart-ot. TCP-alapú remoting transport (`ITransport` / `TTcpTransport`) location-transparent `Send` routinggal. Deliverable: ~80+ új xUnit teszt, persistencia + remoting end-to-end működéssel.

2. **Capability registry + Kernel aktorok (M2–M3):** `TCapabilityRegistry` CST-alapú capability token-ekkel (CFPU-n HW-managed, .NET hoszton szoftveresen), `TRouter` aktor location-transparent címfeloldáshoz, `TRootSupervisor` boot szekvencia. Deliverable: capability-alapú üzenet routing revocation + delegation támogatással, ~70+ új teszt.

3. **Első device aktorok + CFPU referencia demó (M3–M4):** `uart_device`, `gpio_device`, `timer_device` aktorok .NET host-on szimulált MMIO réteggel. Első end-to-end demó: elosztott számláló 4 szimulált core-on keresztül, a CLI-CPU referencia szimulátoron keresztül (a készülő `FenySoft.CilCpu.Sim` NuGet csomaggal). Deliverable: reprodukálható referencia alkalmazás dokumentációval, ~60+ új teszt, 3–5 konkrét HW követelmény `osreq-to-cfpu` issue-ként iktatva. (A hot code loading **kifejezetten elhalasztva követő pályázatra** — komplexitása meghaladja, ami 12 hónapon belül a többi deliverable mellett elfér.)

**Miért releváns az NGI ökoszisztéma számára:**

- **Apache-2.0, teljesen libre:** Permisszív licensz, ami kompatibilis a szélesebb .NET ökoszisztémával — az enterprise adoption útvonala tiszta.
- **Egy nagy, létező .NET fejlesztői bázis célozhatja meg ezt a runtime-ot ismerős eszközökkel:** C#, F#, VB.NET mind CIL-re fordul. A Microsoft éves fejlesztői felmérései és az Akka.NET / Orleans éles deployment-ek mutatják a runtime beágyazott pozícióját az enterprise szoftverben — különösen a szabályozott iparágakban (pénzügy, kormányzat, egészségügy), ahol a Symphact biztonsági modell a legértékesebb. A runtime már ma fut bármely .NET host-on (Windows, Linux, macOS) — a szilícium-függetlenség azt jelenti, nincs hardveres blokkoló.
- **Capability-alapú biztonság framework szinten:** Ellentétben a POSIX jogosultságokkal, a Symphact-nek nincs globális namespace-e. Egy aktor csak akkor küldhet üzenetet másiknak, ha birtokolja a capability-t — nincs ambient authority. Ez a seL4/CHERI biztonsági modell userland .NET-ben. A pusztán szoftveres biztonsági intézkedések egyre kevésbé elegendők a supply chain támadások (log4j, xz-utils), AI-generált exploit-ok és állam-szintű fenyegetések ellen — a capability biztonság konstrukciósan kiküszöböli a sebezhetőségek egész osztályait.
- **Nyílt szilíciummal közösen tervezve:** Bár a Symphact már ma fut bármely CIL host-on, a Cognitive Fabric Processing Unit (CFPU) projekttel közösen van tervezve ([github.com/FenySoft/CLI-CPU](https://github.com/FenySoft/CLI-CPU)). Az OS követelmények visszahatnak a hardver tervezésre az `osreq-to-cfpu` issue template-en keresztül — az Apple M-sorozat OS/hardver integrációjához hasonló kétirányú hurok, de teljesen nyíltan.
- **Formálisan verifikálható:** Az aktor modell matematikai alapjai (CSP, pi-calculus, Erlang bizonyított runtime-ja) természetes formális verifikációs célponttá teszik a Symphact-t — ellentétben a ~40M soros Linux kernel C kóddal.
- **Európai szuverenitás:** Nyílt, auditálható .NET runtime, US/ázsiai IP függőségektől mentes — az Apache-2.0 licensz bármely európai entitásnak engedi fork-olni, módosítani és tanúsítani.

**Miért most?** Három konvergáló erő teszi kritikussá ezt a pillanatot:

1. **Hardver trend:** A 100+ core-os chipek elterjedtek (AWS Graviton4, Ampere AmpereOne, Apple M-sorozat), és a shared-memory skálázás falba ütközött. Az aktor runtime-ok lineárisan skálázódnak a core-számmal.
2. **Nyílt szilícium érettség:** Tiny Tapeout, eFabless, IHP MPW először teszik életképessé az egyedi aktor-natív szilíciumot — a párhuzamos CLI-CPU projekt ezt egy teljesen tesztelt CIL-T0 referencia szimulátorral és előzetes RTL-lel demonstrálja. A Symphact biztosítja a szoftveres célt.
3. **Szabályozói környezet:** EU CRA (Cyber Resilience Act, 2024), IEC 62443, ISO 21434 mind a bizonyítható biztonsági tulajdonságok felé tolnak. Az aktor + capability modell natívan tanúsítható; a Linux (~40M sor C kód) nem.

---

## Volt-e korábbi tapasztalatod releváns projektekkel vagy szervezetekkel?

A pályázó 35+ év professzionális szoftver- és hardvertapasztalattal rendelkezik:

- **Országos szintű éles rendszerek (1990-es évek–2026):** 3 fős csapatban fejlesztett „Atlasz" — vasúti menetirányító rendszer a MÁV számára, amelyet az országos forgalomirányításban 2026-ig használtak. Szintén Visual Restaurant / Visual Hotel & Restaurant szoftvercsomag (Delphi, később .NET), széles körben használt a magyar vendéglátóiparban.
- **.NET ökoszisztéma (20+ év):** Professzionális C#/.NET fejlesztés, beleértve kötelező hatósági adatszolgáltatási integrációkat (NAV adóhatóság, NTAK turizmus), Akka.NET aktor rendszereket (QCassa/JokerQ: 55+ éles aktor), Avalonia UI cross-platform alkalmazásokat, és Android/iOS telepítést.
- **Magyar Adóügyi Ellenőrző Egység (AEE):** Az eredeti AEE projekt ([48/2013 NGM](https://njt.hu/jogszabaly/2013-48-20-2X)) szoftverének egyedüli fejlesztője. A jelenlegi utód **QCassa/JokerQ** **LMS hash-alapú aláírásokkal (NIST SP 800-208 stateful hash-based signature scheme — kvantum-rezisztens a SHA-256 kollízióellenállása révén)** védett modern helyettesítő, 55+ Akka.NET aktor supervision hierarchiában, szintén egyedül fejlesztve a [8/2025 NGM rendelet](https://njt.hu/jogszabaly/2025-8-20-2X) szerint. Ez a projekt mélyreható, gyakorlati tapasztalatot ad éles aktor-modell architektúrában és hash-alapú aláírás-integrációban, ami közvetlenül befolyásolja a Symphact AuthCode tervezését (aláíró- + bytecode-blacklist ugyanazzal az LMS-családdal).
- **Hardver közös-tervezési tapasztalat:** Párhuzamos fejlesztés a **CLI-CPU / CFPU** nyílt szilícium projekten ([github.com/FenySoft/CLI-CPU](https://github.com/FenySoft/CLI-CPU)) — 250+ xUnit teszt a referencia szimulátoron, 48 CIL-T0 opkód, működő Roslyn-alapú linker, előzetes Verilog RTL (ALU modul: 41/41 cocotb teszt). Ez a hardveres projekt külön NLnet pályázatot folytat (beadva 2026-06-01), és biztosítja a kétirányú OS↔HW tervezési hurkot.

A Symphact projekt 2026 áprilisában kezdődött. A beadáskor (2026 május) **142 zöld xUnit teszt** fedi le M0.1–M0.4-et: aktor mag, aktor-közi üzenetküldés, supervision (let-it-crash OneForOne / AllForOne stratégiákkal, lifecycle hookok, hierarchia), és a per-aktor parallelizmusú scheduler API (`TInlineScheduler` + `TDedicatedThreadScheduler` CFPU „1 core = 1 actor" szimulációval). Megközelítőleg ~50 óra fókuszált TDD munka szállította ezt az alapot — **ez a velocity dokumentálva van a roadmap-ben** ([`docs/roadmap.md`](roadmap.md)) és alapot ad a mérföldkő-becslésekhez ebben a pályázatban.

---

## Kért összeg

**€30,000**

## Mire megy a kért összeg

> **Megjegyzés a scope-hoz:** Az M0.3 supervision és M0.4 scheduler már kész (2026 május, ~50ó saját finanszírozásból), és **nem** a pályázat finanszírozza. Az alábbi mérföldkövek a hátralévő munkát fedik le egy használható, biztonságos, perzisztens aktor runtime, kernel aktorok, device aktorok és CFPU integrációs demó eléréséhez.

| Mérföldkő | Leírás | Óra | Összeg | Időkeret |
|-----------|--------|-----|--------|----------|
| **M1: Persistencia** (roadmap M0.5) | Event-sourcing journal (`IPersistenceProvider`, `TInMemoryJournal`, `TSqliteJournal`), snapshot + replay az M0.3 supervision lifecycle-jával integrálva. ~30+ új xUnit teszt. | ~80ó | €2,900 | 1-3. hónap |
| **M2: Remoting + Capability Registry** (roadmap M0.6 + M2.5) | TCP transport (`ITransport` / `TTcpTransport`), serializáció, location-transparent `Send` routing, CST-alapú capability registry delegation + revocation támogatással. ~80+ új teszt. | ~160ó | €5,800 | 2-7. hónap |
| **M3: Kernel + Device aktorok** (roadmap M2.1, M2.3, M3.1-M3.4) | `TRootSupervisor` boot szekvencia, `TRouter` aktor, device aktor framework + `uart_device`, `gpio_device`, `timer_device` szimulált MMIO-n. (Hot code loading kifejezetten elhalasztva követő pályázatra.) ~80+ új teszt. | ~180ó | €6,500 | 5-10. hónap |
| **M4: CFPU integrációs demó** (roadmap M0.7 részleges) | End-to-end demó: elosztott számláló aktor szimulált core-okon keresztül a `FenySoft.CilCpu.Sim` NuGet (CLI-CPU C# referencia szimulátor) segítségével. 3-5 konkrét HW követelmény felfedezése, `osreq-to-cfpu` issue-ként iktatva. | ~80ó | €2,900 | 8-11. hónap |
| **M5: Developer experience + dokumentáció + outreach** | NuGet csomag publikáció (`Symphact.Core`, `Symphact.Persistence`, `Symphact.Remoting`, `Symphact.Security`, `Symphact.Devices`), `symphact` CLI eszköz, angol architektúra dokumentáció, contribution guide, 3 blogposzt, lightning talk egy .NET konferencián. **Contributor növekedés vállalás:** cél 3+ külső contributor 12. hónapra, `CONTRIBUTORS.md`-ben követve. | ~180ó | €6,500 | Folyamatos |
| **M6: Biztonsági audit + formális alapozás** (roadmap M5.4 részleges) | Külső biztonsági review a capability mechanizmusra (független reviewer, €2,000 alvállalkozói). Initial formális specifikáció a `send` / `receive` szemantikára TLA+ vagy Dafny-ben. | ~80ó | €5,400 | 9-12. hónap |
| **Összesen** | | **~760ó** | **€30,000** | **12 hónap** |

**Költségstruktúra:**
- Személyi költség: ~760 óra részmunkaidő × €36/óra ≈ €27,500 (konzisztens a párhuzamos CLI-CPU pályázat óradíjával; ~63ó/hónap részmunkaidő, illeszkedik az M0.1–M0.4 tényleges velocity-hez)
- Biztonsági audit alvállalkozói: €2,000 (külső reviewer a capability mechanizmusra)
- Konferencia utazás (1 lightning talk, EU-n belül): €500
- **Nincs hardver költség** — a Symphact teljesen szoftveres host-okon fut (Windows/Linux/macOS/CI)

---

## Meglévő finanszírozási források

A projekt jelenleg a pályázó saját finanszírozásából működik. Nem érkezett külső finanszírozás. Nincs más függőben lévő pályázat **erre a munkára (Symphact runtime)**.

**Kapcsolódó, de scope-ban elhatárolt:** Párhuzamos NLnet NGI Zero Commons Fund pályázat került beadásra 2026-04-14-én a **CLI-CPU / CFPU hardveres projektre** ([github.com/FenySoft/CLI-CPU](https://github.com/FenySoft/CLI-CPU)) — **ugyanaz a 13. nyílt kör**, mint ez a Symphact beadás. A párhuzamos pályázás teljes átláthatósága szándékos. A két projekt **szándékosan nem átfedő scope-pal** rendelkezik:

| Dimenzió | CLI-CPU / CFPU | Symphact |
|----------|---------------|-----------|
| **Deliverable** | Hardveres ISA, RTL, silicon tape-out, FPGA | Szoftveres runtime, OS szolgáltatások |
| **Cél** | Verilog szintézis, Sky130 PDK | .NET 10 library (fut Windows/Linux/macOS-en) |
| **Licensz** | CERN-OHL-S-2.0 (reciprocal hardver) | Apache-2.0 (permisszív szoftver) |
| **Repository** | `FenySoft/CLI-CPU` | `FenySoft/Symphact` |
| **Finanszírozott mérföldkövek** | F2 RTL, F3 Tiny Tapeout, F4 FPGA multi-core | M0.3-M3.2 aktor runtime + kernel aktorok |
| **Függőségek** | Nincs Symphact-en | Nincs a CLI-CPU szilíciumon (szimulátor elég) |

**Fenntarthatósági terv:**

1. **.NET Foundation submission (9. hónap):** Pályázat a .NET Foundation project membership-re — code signing, CLA management, legal/IP támogatás, Azure infrastruktúra hosting (nem direkt finanszírozás, de közösségi legitimitás).
2. **Követő NLnet pályázat (12-14. hónap):** M0.5-M1.0 + elosztott aktor modell (roadmap 3-4. fázis).
3. **GitHub Sponsors / Open Collective (6. hónaptól):** Közösségi finanszírozási csatornák kiépítése folyamatos karbantartásra. Cél: €500-1500/hónap stabil állapot 2. év végére.
4. **Kereskedelmi tanácsadás (2. évtől):** A FenySoft Kft. integrációs tanácsadási szolgáltatásokat nyújt szabályozott iparágaknak (egészségügy, pénzügy, kritikus infrastruktúra), ahol aktor-modell architektúrára van szükség. Ez keresztfinanszírozást biztosít a core runtime karbantartására, anélkül hogy kompromittálná a nyílt forráskódú magot.
5. **Dual licensing specializált komponensekre (opcionális, 3. évtől):** A core runtime Apache-2.0 marad; enterprise support, formálisan verifikált komponensek és iparág-specifikus bővítmények kaphatnak kereskedelmi licenszet — csak ha kereslet tényleg felmerül.

A projektnek **útvonala van az önfenntartáshoz a 3. évre**, folyamatos grant-finanszírozásra való támaszkodás nélkül.

---

## Összehasonlítás meglévő megoldásokkal

| Projekt | Megközelítés | Korlát | Symphact különbség |
|---------|-------------|--------|--------------------|
| **Akka.NET** | Aktor framework .NET-en | Linux/Windows-on fut globális GC-vel, POSIX jogosultságok, nincs capability biztonság | Capability-alapú biztonság, per-core GC modell, aktor-natív HW-rel közös-tervezve |
| **Microsoft Orleans** | Virtuális aktor framework | Felhő-skála, stateless aktor fókusz, nem OS szintű | OS szintű aktorok device driverek és kernel szolgáltatások szintjén |
| **Erlang/OTP** | Aktor VM supervisionnel | Dinamikusan típusolt, nem .NET ökoszisztéma, saját nyelv | .NET ökoszisztéma (C#/F#/VB.NET), statikusan típusolt, CIL fordítási cél |
| **seL4** | Formálisan verifikált microkernel | Csak C, nincs .NET, kicsi fejlesztői közösség | .NET runtime, capability biztonság mint OS primitív, fejlesztői ergonómia |
| **CHERI / Morello** | Capability architektúra kiterjesztés | ARM/RISC-V specifikus, még akadémiai | Szoftver-first, capability runtime-ban — ma bármely CIL host-on működik |
| **Singularity (Microsoft Research, 2003)** | Típus-biztos OS C#-ban | Csak kutatás, elhagyott, nincs közösség | Production-grade aktor runtime, nyílt HW-vel közös-tervezve |
| **Redox OS** | Microkernel Rust-ban | Csak Rust, nincs .NET, nem aktor-alapú | Aktor-alapú, .NET ökoszisztéma |
| **Tock** | Beágyazott OS Rust-ban | Beágyazott fókusz, nem aktor-alapú | Aktor modell, általános célú |

**Egyetlen létező projekt sem kombinálja:** capability-alapú biztonság + aktor runtime + .NET ökoszisztéma + nyílt forráskód + nyílt szilíciummal közös-tervezve. A Symphact egy új pozíció ebben a térben.

---

## Jelentős technikai kihívások

1. **Capability token hamisítás-ellenállás:** A `TActorRef` struct egy opaque CST (Capability Slot Table) index. CFPU hardveren a capability-k HW-managed QRAM-ban (Quench-RAM) élnek; .NET hoszton a kihívás a capability tokenek generálása, verifikálása és visszavonása trusted execution environment nélkül. Mitigáció: CST-alapú capability registry aktor runtime-managed slot allokációval; formális specifikáció a threat model-re.

2. **Supervisor restart szemantika shared-memory host-okon:** Az Erlang "restart"-ja feltételezi, hogy az aktor állapot izolált (külön heap). Egy .NET shared-GC host-on az aktor újraindítása gondos állapot-tisztítást igényel cross-actor szivárgások megelőzésére. Kihívás: tiszta restart határok teljesítmény kompromittálása nélkül. Mitigáció: per-actor arena allokátorok (ArrayPool-backed) + explicit állapot szerializáció az M0.5-ben.

3. **Determinisztikus scheduler formális verifikációhoz:** Az aktor modell TLA+ specifikációi determinisztikus schedulingot igényelnek, hogy hasznosak legyenek. Kihívás: determinizmus összeegyeztetése multi-thread végrehajtással. Mitigáció: `TDedicatedThreadScheduler` per-actor thread izolációt biztosít; `TRoundRobinScheduler` determinisztikus single-threaded végrehajtást verifikációs futtatásokhoz.

4. **Persistencia + supervisor restart interakció:** Az event sourcing-nak tisztán kell integrálódnia a `PreRestart` / `PostRestart` lifecycle hookokkal — a replay-nek be kell fejeződnie, mielőtt az aktor új üzeneteket fogad, különben a restart versenyt fut az élő forgalommal. Kihívás: tiszta „recovering" sub-state definiálása a FIFO invariáns megsértése nélkül. Mitigáció: üzenet-stash a replay alatt; explicit `RecoveryCompleted` jelzés az M0.5 spec-ben.

5. **OS-HW követelmény felderítés fegyelem:** Az `osreq-to-cfpu` hurkának konkrét, cselekvőképes HW követelményeket kell produkálnia (nem wishlist tételeket). Kihívás: „nice-to-have" elkülönítése a „required for correctness"-től. Mitigáció: minden `osreq` issue-nek tartalmaznia kell egy benchmark mérést a szimulátorról, ami bizonyítja a szükségletet — nincs követelmény adat nélkül.

---

## A projekt ökoszisztémája

**Upstream függőségek (mind nyílt forráskódú):**
- **.NET 10 SDK** (Microsoft, MIT licensz) — runtime, compiler, test framework
- **xUnit 2.9.3** — tesztelési framework
- **CLI-CPU referencia szimulátor** (CERN-OHL-S-2.0) — a készülő `FenySoft.CilCpu.Sim` NuGet-en keresztül CFPU integrációs demókhoz
- **Visual Studio Code / Code - OSS** (MIT) — elsődleges fejlesztői környezet

**Downstream felhasználók és stakeholderek:**
- **.NET fejlesztői közösség (~8M+ fejlesztő):** Bármely C#/F# kódbázis adoptálhatja a Symphact-t aktor runtime-ként. Az Akka.NET felhasználók természetes korai adoptálók (az API hasonlóság szándékos).
- **Beágyazott / IoT fejlesztők:** A device actor modell + capability biztonság biztonságosabb alternatíva az RTOS-ekkel (FreeRTOS, Zephyr) szemben szabályozott domain-ekben.
- **Biztonság-tudatos szektorok:** Egészségügy, kritikus infrastruktúra, autóipar (ISO 26262), orvosi eszközök (IEC 62304), ipari vezérlés (IEC 61508) — ahol a Linux tanúsítása 10+ évet vesz igénybe.
- **CLI-CPU / CFPU hardveres projekt:** A Symphact az elsődleges szoftveres célpont a nyílt szilícium munkához, konkrét HW követelményeket produkálva az `osreq-to-cfpu`-n keresztül.
- **Formális módszerek kutatói közösség:** TLA+ / Dafny specifikációk a Symphact-t oktatási és kutatási célponttá teszik.
- **Európai digitális szuverenitás iniciatíva:** Apache-2.0 licensszel, teljesen auditálható, US/ázsiai IP függőségektől mentes.

**Közösségépítési terv:**
- **GitHub repository CI/CD-vel:** Minden commit lefuttatja az összes tesztet; zöld badge látható a README-n.
- **NuGet csomag publikáció:** `Symphact.Core` publikálva a nuget.org-on a 4. hónapra.
- **Dokumentációs weboldal:** GitHub Pages site tutoriállal, API referenciával, tervezési indoklással.
- **Blogposztok:** 3 technikai mérföldkő poszt + 1 év végi retrospektív.
- **Konferencia outreach:** Lightning talk a .NET Conf-on, Update Conf-on, vagy más EU-alapú .NET eseményen.
- **`osreq-to-cfpu` issue template:** HW/OS közös-tervezési hozzájárulást tesz lehetővé.
- **Havi progress report** a projekt weboldalán.

**Contributor növekedés vállalás (egyszemélyes pályázói kockázat mitigációja):** A Symphact jelenleg egy egy-fejlesztős projekt — ez az NLnet által dokumentált kockázati tényező. Ennek kezelésére a repository fenntart egy `CONTRIBUTORS.md` fájlt, amely felsorolja az összes lényegi technikai contributor-t (runtime szerzők, teszt szerzők, dokumentáció karbantartók — nem csupán typo-fix PR-ek). A cél **3+ lényegi külső contributor a 12. hónapra**. Az onboarding-fókuszú feladatok kifejezetten `good-first-issue` és `help-wanted` címkével vannak ellátva az issue tracker-ben.

**Támogató levelek:** A követő pályázathoz tervezzük egy akadémiai levél (BME Elektronikus Eszközök Tanszéke vagy SZTAKI) és egy ipari levél (.NET Foundation tag, Akka.NET maintainer vagy Tiny Tapeout mentor) csatolását, dokumentálva az intézményi támogatást.

**Nem-célok (explicit scope határok):**
- **NEM Linux helyettesítő ebben a pályázati időszakban.** A hosszú távú vízió 10-20 éves átmenetet említ; ez a pályázat *egy működő aktor runtime-ot céloz capability biztonsággal*, nem desktop/szerver helyettesítést.
- **NEM bare-metal hardver kernel ebben a pályázati időszakban.** .NET host OS-en fut; a CFPU-natív végrehajtás egy követő fázis.
- **NEM elosztott rendszer ebben a pályázati időszakban.** A location transparency primitívek M2-ben landolnak, de a több-node-os elosztás a 3. fázis (követő pályázat).

---

## .NET függetlenség és szabvány illesztés

A CIL specifikáció (ECMA-335) egy ISO/IEC 23271 által ratifikált nemzetközi szabvány. A Symphact a bytecode formátumot célozza, nem valamely saját Microsoft runtime-ot. Alternatív CIL implementációk léteznek (Mono, örökölt .NET Framework compilerek, különböző Roslyn-független front-endek). A runtime design CIL szinten működik, és független bármely upstream runtime változástól.

**Apache-2.0 licensz** permisszív felhasználást biztosít bármely downstream projektben, beleértve a kereskedelmi felhasználást — konzisztens a .NET ökoszisztéma normáival és a .NET Foundation projekt elvárásokkal.

---

## Melléklet terv

PDF mellékletek (~15-20 oldal összesen):
1. **Architektúra áttekintés** — kivonat a `docs/vision-en.md`-ből (capability modell, aktor hierarchia, üzenet routing)
2. **Roadmap** — `docs/roadmap.md` Fázis 1-7 óra becslésekkel
3. **Jelenlegi állapot snapshot** — 142 xUnit teszt output (M0.1–M0.4 ✅), kód metrikák, repo screenshot
4. **CLI-CPU ↔ Symphact interakciós diagram** — OS követelmények visszacsatolási hurok
5. **Threat model összefoglaló** — capability hamisítás, supervisor escape, hot-load tampering
6. **1-oldalas executive summary** — probléma, megközelítés, deliverable-ök, költségvetés, fenntarthatóság

---

## Belső megjegyzések (NEM a pályázat része — csak nekünk)

### Miért €30K és nem €50K?
- Az első NLnet pályázat általában €30-50K — a €30K reálisabb és **jobb elfogadási eséllyel**
- A €30K fedezi az M0.3-M3.2-t + első CFPU demót + biztonsági auditot — ez **mérhető, demonstrálható eredmény** 12 hónap alatt
- Ha sikeres, a második pályázat (€50-150K) fedezheti az M0.5-M1.0 + distributed actor model (Phase 3-4) munkákat

### Miért erősebb ez, mint a CLI-CPU v1.1 draft?
- **Kisebb, fókuszáltabb scope** — csak szoftver, nincs hardveres bizonytalanság (tape-out shuttle csúszás, TT tile méret kockázat)
- **Világosabb scope elhatárolás** a CLI-CPU-tól a sustainability plannel együtt
- **Konkrétabb sustainability plan** — .NET Foundation, GitHub Sponsors, FenySoft consulting, dual license
- **Biztonsági audit explicit** — külső reviewer bevonása, ami erősíti a capability biztonsági narratívát
- **Formális alapozás** — TLA+ vagy Dafny, ami NLnet reviewer-eknek különösen vonzó

### Kockázatok és mitigáció
| Kockázat | Valószínűség | Mitigáció |
|----------|-------------|-----------|
| Külső biztonsági reviewer nem található €2K-ért | Közepes | NLnet belső kapcsolatokon keresztül (korábbi seL4 / CHERI grantee-k) |
| Hot code loading túl komplex 12 hónap alatt | Alacsony | M3.2-ben csak foundation, teljes implementáció a követő pályázatban |
| .NET Foundation elutasítja 9. hónapban | Alacsony | Fallback: Eclipse Foundation, Linux Foundation sandbox |
| CFPU referencia szimulátor NuGet csúszik | Közepes | A Symphact közvetlenül is tud a CLI-CPU forráskódra hivatkozni — nem kritikus függőség |

### Következő lépések (2026-06-01 deadline-ra)
1. **M0.3 + M0.4 KÉSZ** — supervision (let-it-crash, OneForOne / AllForOne) és scheduler (`TInlineScheduler` + `TDedicatedThreadScheduler`) leszállítva, 142 zöld teszt
2. **Symphact README frissítése** — aktuális státusz (M0.1–M0.4 ✅, M0.5 következő), 142 teszt, roadmap update
3. **PDF mellékletek elkészítése** előre (architektúra, roadmap, status snapshot, CLI-CPU↔Symphact diagram, threat model, 1-page exec summary)
4. **.NET Foundation / Akka.NET maintainer** — informális kapcsolatfelvétel követő pályázat support letter-hez (ehhez a beadáshoz nem kötelező, de erősíti a sustainability narratívát)
5. **Beadás** — https://nlnet.nl/propose/ — **13. nyílt kör, deadline 2026-06-01, 12:00 CEST**

---

## Changelog

| Verzió | Dátum | Összefoglaló |
|--------|-------|-------------|
| 1.1 | 2026-05-01 | Beadás-kész változat a 13. nyílt körre (deadline 2026-06-01, ugyanaz a kör, mint a párhuzamos CLI-CPU beadás). Az M0.3 supervision és M0.4 scheduler tudomásul véve mint már leszállított (pályázati scope-on kívüli). Új mérföldkő-struktúra: M1 Persistencia, M2 Remoting + Capability Registry, M3 Kernel + Device aktorok (hot code loading elhalasztva követő pályázatra), M4 CFPU integrációs demó, M5 DX + outreach + contributor növekedés, M6 Biztonsági audit + formális alapozás. Költségvetés-aritmetika javítva: 760ó × €36 ≈ €27,500 személyi + €2,000 audit + €500 konferencia = €30,000. CLI-CPU corrections #5 (developer-base grounding) és #9 (egyszemélyes pályázói kockázat + supporting letters) beépítve. „Miért most?" deduplikálva. Tesztszám frissítve 46 → 142. |
| 1.0 | 2026-04-23 | Kezdeti draft. Scope: M0.3-M3.2 + CFPU demó + biztonsági audit. Költségvetés €30,000 / 12 hónap / ~340ó. Scope-elhatárolva a CLI-CPU pályázattól (hardver vs szoftver). |
