# NLnet NGI Zero Commons Fund — Pályázati vázlat (Symphact)

> **Deadline:** 2026-06-01, 12:00 CEST  
> **Form:** https://nlnet.nl/propose/  
> **Kiírás:** NGI Zero Commons Fund — 13. nyílt kör (ugyanaz a kör, mint a párhuzamos CLI-CPU beadás 2026-04-14-én)  
> **Státusz:** DRAFT — beadás-előkészítés a 2026-06-01-i határidőre  
> **Megjegyzés:** A pályázat **angolul kerül beadásra** (NLnet követelmény). Ez a magyar változat tájékoztató jellegű. A beadandó angol verzió: `nlnet-application-draft-en.md`.  

> English version: [nlnet-application-draft-en.md](nlnet-application-draft-en.md)

---

## Pályázati kiírás

NGI Zero Commons Fund

## Pályázat neve

**Symphact: aktor-alapú operációs rendszer a CFPU-val közösen tervezve**

## Weboldal / Wiki

https://github.com/FenySoft/Symphact

---

## Kivonat

**Mit kérünk röviden:** A Symphact egy **capability-alapú aktor runtime .NET-re**, **Apache-2.0** licensszel, együtt-tervezve a Cognitive Fabric Processing Unit (CFPU) nyílt szilícium projekttel ([`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU), CERN-OHL-S-2.0). **Beadáskor (2026-05-23): 186 zöld xUnit teszt** fedi le az aktor magot, supervision (let-it-crash), per-aktor parallel scheduler-t (CFPU dedikált-core-per-aktor szimuláció), és a BCL-only event-sourcing journal + snapshot store-t — szigorú TDD-vel, ~65 óra fókuszált munkából, LLM-asszisztált ügynök-csapat módszertannal (lásd „Munkamódszer és transzparencia"). A 12 hónapos **€30,000-os grantet** a content-addressed perzisztencia, TCP remoting + capability registry, kernel + device aktorok, CFPU integrációs demó, formális spec-alapozás (TLA+), és outreach finanszírozására kérjük. A pályázó **35+ év professzionális szoftver- és hardvertapasztalattal** rendelkezik: Atlasz MÁV menetirányító 26 év éles üzem; AEE/QCassa magyar adóhatóság-tanúsított pénztárgépszoftver 55+ Akka.NET aktorral; CLI-CPU referencia szimulátor 250+ teszttel. A projekt **hosszú távú víziója** — a post-Dennard számítási paradigmához tett első mérnöki lépés — az alábbi szekciókban kerül kifejtésre.

---

**A jelenlegi számítási stack — Linux + GPU + gyorsítók + cache-koherencia — a fizikai korlátok határán működik.** A Dennard-skálázás a 2000-es évek közepén megállt (Bohr, IEEE 2007; Esmaeilzadeh et al., ISCA 2011), a koherens megosztott memória néhány száz mag fölött egyre költségesebbé és nehezebben skálázhatóvá válik (MOESI/MESIF overhead, „dark silicon"), az AI-modellek tanításának energiaigénye továbbra is magas ($100M+ egyetlen frontier modellre — a hatékonyság-trendek mellett is), a heterogén CPU+GPU+NPU+TPU stack minden generációval bonyolódik. **A következő 15–20 évben a számítás paradigmája valószínűleg megváltozik:** share-nothing, üzenet-átadás, egyetlen homogén alapegység, aktoronként dedikált mag — ez az egyetlen út, amely megkerüli a fizikai korlátokat (nincs koherencia, nincs közös busz, lineáris skálázás a magok számával).

A Symphact + CFPU ennek az új paradigmának az **első nyíltan dokumentált, működő architekturális prototípusa**: egyetlen alapegység (`TActor`), amely klasszikus OS-folyamatként, AI-ágens runtime-ként és hardvermeghajtóként **egyazon homogén magrácson** szolgál. A meglévő próbálkozások mind **egyetlen szegmenst** céloznak (Intel Loihi = csak SNN; Akka.NET = csak userland; seL4 = csak biztonság; Linux = csak klasszikus OS) — **a homogén integráció Symphact saját terepe**.

A projekt befejezte az M0.1–M0.4 mérföldköveket és félúton jár az M0.5-ben: **186 zöld xUnit teszt** fedi le a core primitíveket — `TMailbox` (FIFO mailbox), `TActorRef` (capability token), `TActor<TState>` (absztrakt aktor), `TActorContext` (handler context), `TActorSystem` (runtime), supervision (M0.3 — `ISupervisorStrategy`, OneForOne / AllForOne, lifecycle hookok, aktor hierarchia), a per-aktor parallelizmusú scheduler API (M0.4 — `IScheduler`, `TInlineScheduler`, `TDedicatedThreadScheduler`, CFPU dedikált-core-per-aktor szimuláció), és a perzisztencia réteg első két szelete (M0.5 — `IJournal` + `TInMemoryJournal`, `ISnapshotStore` + `TInMemorySnapshotStore`, BCL-only referencia implementációk). Mindezt szigorú TDD módszertannal, .NET 10-en, Apache-2.0 licensszel. **Az M0.3 supervision, M0.4 scheduler és az M0.5 perzisztencia BCL-only referencia szeletei a v0.1 (2026 április) és a beadás között elkészültek, és kifejezetten kívül esnek e pályázat scope-ján — a pályázat a content-addressed produkciós szintű journalt, a supervision lifecycle-lal való integrációt és az M0.5 többi részét finanszírozza.**

**A pályázat egyszerre mérnöki, kutatási és paradigmatikus karakterű.** A perzisztencia-, remoting- és device-aktor-rétegek mérnöki deliverable-ek. Mellettük 9 nyitott kutatási kérdést kezdünk el megválaszolni — ezekre **nincs üzemi szintű precedens**, mert aktoronként dedikált magos hardver eddig nem létezett. **És** a 12 hónap **a következő számítási paradigma első mérnöki rétegét** rakja le: nem a végleges optimumot, hanem azt az alapot, amelyre a következő 20–30 év kohortája épít. **Ez nem 12 hónapos verseny — ez egy paradigmaváltási út első mérnöki köve, dokumentált bizonyítékkal arra, hogy a paradigma fizikailag megvalósítható.**

**A vízió és a deliverable közötti tudatos szétválasztás:** A „post-Dennard paradigma 15–30 éves útja" és az „egyesítő tézis" a projekt **hosszú távú iránya és kutatási kerete**. A jelen pályázat **12 hónapra szóló része** ennek a víziónak az **első mérnöki fundamentumát** szállítja — konkrétan: content-addressed perzisztencia (M1), location-transparent remoting + capability-registry (M2), 3 device-aktor + 1 LLM-vezérelt agent-aktor + 1 LIF-neuron-aktor a CFPU-szimulátoron (M3–M4), audit + formális alapozás (M6), kétnyelvű dokumentáció és outreach (M5). A **vízió a kontextus**, a **deliverable-ek a méréses fundamentum**. A mismatch (víziós ambíció vs. 12 hónapos méret) **tudatos**: ez az **első lépés**, nem az utolsó.

**Miért most?**

A jelenlegi operációs rendszerek (Linux, Windows, macOS) az 1970-es években hozott architekturális döntéseket öröklik — megosztott memória, monolitikus kernel, POSIX-jogosultságok, fork/exec, cache-koherencia mint láthatatlan alapfeltevés. Ezek a megoldások **fizikailag falba ütköznek**: egy chipen belül néhány száz mag fölött a koherencia-ráfordítás szignifikánssá válik („dark silicon" jelenség), a frekvenciaskálázás a 2000-es évek közepe óta áll, az AI-tréning energiaigénye egyetlen modellre elérte a $100M-t (a hatékonyság-trendek — DeepSeek V3, MoE, distillation — mérséklik, de nem fordítják meg az alaptrendet), és a heterogén CPU+GPU+NPU+TPU+gyorsító stack minden generációval bonyolódik. Joe Armstrong (az Erlang megalkotója) több előadásában és írásában megfogalmazta, hogy az aktor-modell ideálisan dedikált hardveren futna — ez a hardver akkor még nem létezett. **Mostanra létezik.** A nyílt szilícium (Tiny Tapeout, eFabless, IHP MPW), az érett aktor-framework-ök (Akka.NET, Orleans, Akka JVM, SpiNNaker), a multi-ágenses AI-rendszerek tömeges megjelenése (Anthropic Agent SDK, OpenAI Agents, LangGraph) és a Dennard-skálázás végének együttes hatása **kényszeríti** a paradigmaváltást — a kérdés már nem az, hogy lesz-e, hanem hogy ki birtokolja.

A Symphact öt bizonyított ötletet kombinál **egyetlen homogén alapegységben**, modern alapokon:

- **Aktormodell** (Erlang/OTP, 40+ év telco/pénzügy) — let-it-crash + supervision
- **Capability-alapú biztonság** (seL4, CHERI) — hamisíthatatlan referenciák, nincs globális névtér
- **Típus-biztos runtime** (Singularity MS Research prototípus, 2003) — memóriabiztonság ISA-szinten
- **Determinisztikus üzenetküldés** (QNX kereskedelmi demonstráció) — formális verifikálhatóság
- **Neuromorf-rokon számítás** (Intel Loihi, IBM TrueNorth, SpiNNaker / EBRAINS) — aszinkron, lokális állapot, üzenetvezérelt; ugyanaz a filozófia, csak típusos üzenet szinten, nem spike-szinten

**Az egyesítő tézis (unified primitive thesis):** ugyanaz a `TActor<TState>` alapegység tölti be klasszikus OS-folyamat, hardvermeghajtó (MMIO-n keresztül), AI-ágens (LLM-vezérelt) és „okos neuron" (aszinkron tanuló entitás) szerepét — egyazon homogén magrácson. Ezt **senki nem építi nyíltan**: Loihi csak SNN, Akka.NET csak userland, seL4 csak biztonság, Linux csak klasszikus OS. A homogén integráció **Symphact saját terepe és a pályázat központi tézise**.

**A pályázat három konkrét eredményt céloz (a már leszállított M0.1–M0.4 + M0.5 első szelet alapra építve):**

1. **Perzisztens, share-nothing alap (M1–M2):** Content-addressed event-sourcing journal (`IPersistenceProvider`, `TCasJournal`, `TCasSnapshotStore` — Git/IPFS-szerű content-addressable tárolás, ahol a fájlnév a blob SHA-256 hash-e, append-only blobok automatikus deduplikációval, BCL-only — nincs külső függőség), snapshot + replay az M0.3 supervision lifecycle-jával integrálva. TCP-alapú remoting transport (`ITransport` / `TTcpTransport`) helyfüggetlen `Send` útválasztással — a **share-nothing üzenet-átadási alap első bizonyítása** valódi hálózati ugrás felett. Deliverable: ~80+ új xUnit teszt.

2. **A homogén alapegység bizonyítása (M2–M3):** `TCapabilityRegistry` CST-alapú capability-tokenekkel (CFPU-n HW-managed, .NET-hoszton szoftveresen), `TRouter` aktor helyfüggetlen címfeloldáshoz, `TRootSupervisor` boot-szekvencia, valamint **`TDeviceActor` (klasszikus szerep), `TAgentActor` (AI-ágens szerep), `TNeuronActor` (neuromorf-rokon szerep)** — mindhárom **ugyanazt a `TActor<TState>` alapegységet** használja, csak konfigurációban és handler-logikában különbözik. Ez az egyesítő tézis első mérhető demonstrációja. Deliverable: ~70+ új teszt + dokumentált benchmark arról, hogy a három szerep ugyanazon a runtime-on fut konzisztens szemantikával.

3. **Első end-to-end, többszerepű demó CFPU-szimulátoron (M3–M4):** Reprodukálható alkalmazás, amely **egyazon Symphact-rendszerben** futtat: (a) klasszikus számítást (elosztott számláló 4 szimulált magon), (b) hardvervezérlést (`uart_device`, `gpio_device`, `timer_device` MMIO-n keresztül), (c) multi-ágenses AI-pipeline-t (LLM-vezérelt `TAgentActor`-ok capability-vel korlátozott interakciói), és (d) neuromorf-rokon kísérletet (`TNeuronActor` aszinkron tanulási minta). Minden a CLI-CPU referencia szimulátoron, a `FenySoft.CilCpu.Sim` NuGet-en keresztül. Deliverable: ~60+ új teszt, 3–7 konkrét HW-követelmény `osreq-to-cfpu` issue-ként iktatva, publikálható blogposzt-sorozat. (A hot code loading **kifejezetten elhalasztva követő pályázatra**.)

E három mérnöki eredmény mellé az M5 mérföldkő **outreach- és dokumentáció-vállalást** hordoz — NuGet publikáció, kétnyelvű architektúra-dokumentáció, 3 blogposzt, 2-3 online technikai bemutató videó, GitHub Pages, havi research log. A **tényleges kohorta-építés** (akadémiai LoI-k, ipari partnerek) **a follow-up pályázat központi deliverable-ja lesz**, amikor a párhuzamos CLI-CPU projekt szállította az első fizikai HW-bizonyítékot (Tiny Tapeout F3, FPGA F4) — addig az NLnet-szabálykonform „contributor növekedési vállalás" kifejezetten ennek a fundamentum-fázisnak felel meg: passzív érdeklődés és kontakt-funnel, nem fizetett vagy aktív hozzájáruló.

**Miért releváns az NGI ökoszisztéma számára:**

- **Apache-2.0, teljesen libre:** Permisszív licensz, ami kompatibilis a szélesebb .NET ökoszisztémával — az enterprise adoption útvonala tiszta. **A FenySoft Verified signing pipeline (CFPU AuthCode) opcionális, nem előfeltétel:** A Symphact runtime **teljes funkcionalitással működik** szoftveres `TCapabilityRegistry`-vel; egy szervezet saját signing infrastructure-rel, alternatív aláíró-trust-anchorral, vagy AuthCode nélkül is deploy-olhatja. A FenySoft cert a CFPU silicon mellé vásárolt **opcionális hardver-szintű hot-load tampering védelem** (lásd `docs/trust-model-hu.md`), nem a Symphact runtime használatának feltétele. Ez konzisztens az AOSP-modellel (Android Open Source Project nyílt; Google Play Services + védjegy zárt), de **Symphact maga a teljes Apache-2.0 stack** — a hardver-szintű enforcement HW-vásárlás kérdése, nem a libre runtime-é. A CLI-CPU RTL alatt (CERN-OHL-S-2.0) bárki gyárthat saját trust-anchor-os chipet, ami **nem FenySoft Verified**, de teljes Symphact-kompatibilis.
- **Egy nagy, létező .NET fejlesztői bázis célozhatja meg ezt a runtime-ot ismerős eszközökkel:** C#, F#, VB.NET mind CIL-re fordul. A Microsoft éves fejlesztői felmérései és az Akka.NET / Orleans éles deployment-ek mutatják a runtime beágyazott pozícióját az enterprise szoftverben — különösen a szabályozott iparágakban (pénzügy, kormányzat, egészségügy), ahol a Symphact biztonsági modell a legértékesebb. A runtime már ma fut bármely .NET host-on (Windows, Linux, macOS) — a szilícium-függetlenség azt jelenti, nincs hardveres blokkoló.
- **Capability-alapú biztonság framework szinten:** Ellentétben a POSIX jogosultságokkal, a Symphact-nek nincs globális namespace-e. Egy aktor csak akkor küldhet üzenetet másiknak, ha birtokolja a capability-t — nincs ambient authority. Ez a seL4/CHERI biztonsági modell userland .NET-ben. A pusztán szoftveres biztonsági intézkedések egyre kevésbé elegendők a supply chain támadások (log4j, xz-utils), AI-generált exploit-ok és állam-szintű fenyegetések ellen — a capability biztonság konstrukciósan kiküszöböli a sebezhetőségek egész osztályait.
- **OS→HW visszacsatolás elsődleges eredményként:** Bár a Symphact már ma fut bármely CIL-hoszton, a Cognitive Fabric Processing Unit (CFPU) projekttel közösen van tervezve ([github.com/FenySoft/CLI-CPU](https://github.com/FenySoft/CLI-CPU)). Az OS-követelmények visszahatnak a hardver tervezésére az `osreq-to-cfpu` issue-sablonon keresztül — kétirányú co-design hurok, amelyre dokumentált történelmi precedens az Inmos Transputer + Occam párosítás (~1984, csatorna-alapú üzenetküldés egyszerre hardver- és nyelvi szinten) és a Symbolics Lisp Machine architektúrája (Moon, „Architecture of the Symbolics 3600", 1985 — tagged pointers és GC-hardvertámogatás a nyelvi runtime számára). A Symphact + CFPU ezek modern, .NET-alapú újragondolása, teljesen nyíltan (Apache-2.0 + CERN-OHL-S licensszel). Minden mérföldkő mérési alszelete potenciálisan **konkrét HW-követelményt** szülhet, dokumentált bizonyítékkal — várt visszahatási útvonalak: a HW mailbox FIFO mélységének konfigurálhatóvá tétele, supervision fault notification primitívek, HW-szintű CST-gyorsítótár, SHA-256 / BLAKE3 hash-utasítás (a content-addressed journalhoz), SRAM-to-SRAM DMA az aktor-állapot migrálásához. **3–7 konkrét `osreq-to-cfpu` issue várható eredmény a 12 hónap alatt** — minden visszahatási döntés Apache-2.0 / CERN-OHL-S licensz alatt publikus és reprodukálható.
- **Formálisan verifikálható:** Az aktor modell matematikai alapjai (CSP, pi-calculus, Erlang bizonyított runtime-ja) természetes formális verifikációs célponttá teszik a Symphact-t — ellentétben a ~40M soros Linux kernel C kóddal.
- **Multi-ágenses AI-infrastruktúra mint közeli kereskedelmi belépési pont:** A transzformer-skálázás várhatóan kifullad ($100M+ egyetlen frontier modell tanításra, exponenciálisan növekvő energiaigény); a piac most fedezi fel, hogy egyetlen LLM helyett **koordináló ágens-hálózatok** kellenek (Anthropic Agent SDK, OpenAI Agents/Swarm, LangGraph, AutoGen). Ezek **pontosan aktor-rendszerek**, csak jelenlegi implementációik kezdetlegesek — függvényhívás + JSON Pythonban. A Symphact **strukturált, capability-vel korlátozott, auditálható multi-ágenses runtime**-ot kínál ugyanarra a feladatra. A 12 hónap egyik bizonyító eredménye egy LangChain-szerű multi-ágenses pipeline Symphact-aktorokon, ahol minden ágens capability-vel korlátozott, auditnaplózható és supervision alatt fut. Ez **2–5 éves piaci ablakot** céloz. *Konkrétan miért nem elég az Akka.NET vagy a Proto.Actor:* mindkettő **userland framework**, megosztott GC-vel és POSIX-jogosultságokkal; egy LLM-vezérelt ágens **nem korlátozható konstrukciósan** a tevékenységében (függvényhívásos sandbox-ot lehet kerülni, JIT-eval-t lehet végrehajtani). A Symphact `TActor` ezzel szemben **capability-vel korlátozott** (egy ágens csak azokat a `TActorRef`-eket birtokolhatja, amelyeket explicit kapott), és a `TActorSystem.Send` runtime + (CFPU-n HW-szinten) validálja a célt — **ez a teljes audit-lánc** strukturált eseményekkel. Ez **kompatibilis irány** szabályozott AI-deployment-ekkel (EU AI Act high-risk kategória elvárt audit-láncára), bár a tényleges tanúsítási megfelelőség csak később, érettebb projektszinten értelmezhető — a Symphact a 12 hónap végén **audit-input substrate**-et szállít (strukturált event-log + capability-graph), nem tanúsított rendszert. **Fontos pontosítás:** a Symphact a tool-szintű autorizációt és audit-láncot teszi konstrukciósan strukturálttá; az LLM-vezérelt ágens **döntési logikája** (mit kér, milyen prompttal) **a Symphact aktor-rendszerén kívül** zajlik (külső HTTP API), így a Symphact **nem** old meg LLM-szintű problémákat (prompt injection, jailbreak, hallucination) — ezek továbbra is az alkalmazás-réteg felelőssége.
- **Európai paradigma-szuverenitás:** Nem inkrementális Linux-fork, hanem a **következő számítási paradigma** európai birtoklásának első mérnöki lépése — még az amerikai-kínai szilíciumháború bezárulása előtt. Apache-2.0 + CERN-OHL-S licenccel, amerikai/ázsiai IP-függőségektől mentes, az NGI küldetésének központi céljához illeszkedve. Az EU Human Brain Project (SpiNNaker, EBRAINS) és a Tiny Tapeout EU-IHP MPW vonalakkal természetes szövetségben.

**Miért most?** Öt egymást erősítő erő teszi kritikussá ezt a pillanatot:

1. **Fizikai korlátok:** A Dennard-skálázás a 2000-es évek közepe óta megállt (Bohr, IEEE 2007), a koherens megosztott memória nagy magszám mellett egyre költségesebbé válik (MOESI/MESIF overhead), és a power/thermal korlátok dark silicon jelenséghez vezetnek (Esmaeilzadeh et al., ISCA 2011); az AI-inferencia és -tréning energiaigénye továbbra is magas és növekvő. **Ez nem ízlés kérdése, hanem mérnöki kényszer:** a következő paradigma valószínűleg share-nothing üzenet-átadás lesz, mert ez **a legtisztább út** a magszám-függő overhead megkerülésére.
2. **Hardvertrend:** A 100+ magos csipek elterjedtek (Ampere AmpereOne 192, Intel Sierra Forest 288, AWS Graviton4 96, NVIDIA Grace 144), és a megosztott memóriás skálázás **diminishing returns**-be ütközött. Az aktor-runtime-ok **közel-lineárisan** skálázódnak a magok számával, a megosztott memóriás szinkronizáció szűk keresztmetszete nélkül.
3. **Nyílt szilícium érettsége:** A Tiny Tapeout, eFabless és IHP MPW először teszi **megfizethetővé** az egyedi szilícium-prototipizálást — a párhuzamos CLI-CPU projekt ezt egy teljesen tesztelt CIL-T0 referencia szimulátorral és előzetes RTL-lel **demonstrálja a szoftveres oldalról**; az aktornatív szilícium kimenetet a CFPU F3-F4 fázisai termelik (várható 2027–2028).
4. **Multi-ágenses AI-piac nyílik:** Anthropic Agent SDK, OpenAI Agents, LangGraph — a piac multi-agent koordinációs igényt fedez fel, amely **strukturális hasonlóságot mutat** az aktor-modellel (üzenet-passzolás, lokális állapot, supervision). A Symphact 2–5 éves piaci ablakban érkezhet **strukturált, capability-vel biztosított runtime**-mal a jelenlegi function-calling alapú megoldások mellé.
5. **Szabályozói környezet:** Az EU CRA (Cyber Resilience Act, 2024), az IEC 62443, az ISO 21434 és az EU AI Act mind a bizonyítható biztonsági és auditálhatósági tulajdonságok felé tol. Az aktor + capability modell **természetes célpont** a formális verifikációra és tanúsításra (seL4-precedens); a Linux (~40M sor C-kód) és a function-calling alapú AI-ágens stack ezekhez nem illeszkedik.

**Történelmi precedens — győztesek és vesztesek:** Aki paradigmaváltást vállalt és nyert: Unix 1969 (kortárs konszenzus: Multics elég), aktormodell 1973 (akadémiai elmélet), Linux 1991 (hobbi-projekt), iPhone 2007 (PDA + telefon = niche). Aki vállalta és vesztett: Symbolics Lisp Machine (1980, túl drága szilícium), Inmos Transputer + Occam (1984, „kevés app szoftver"), Intel iAPX 432 (1981, kortárs OO túl lassú), Microsoft Singularity Research (2003, akadémiai marad). A különbség nem a víziós ambíció méretében volt — **mindegyik vesztes is okos volt** —, hanem **(a) a fejlesztői ökoszisztéma érettségében**, **(b) az időzítésben** a hardver-érettséghez képest, és **(c) a fenntartható közösségépítésben** a víziós úttörő után. A Symphact ezekhez a három tengelyhez illeszkedik: ~8M létező .NET fejlesztő ökoszisztéma; a nyílt szilícium (Tiny Tapeout, IHP MPW) most először elérhető; és a kohorta-építés **explicit M5 deliverable**, nem mellékhatás.

---

## Volt-e korábbi tapasztalatod releváns projektekkel vagy szervezetekkel?

A pályázó 35+ év professzionális szoftver- és hardvertapasztalattal rendelkezik:

- **Országos szintű éles rendszerek (1990-es évek–2026):** 3 fős csapatban fejlesztett „Atlasz" — vasúti menetirányító rendszer a MÁV számára, amely 2000 óta folyamatosan, az országos forgalomirányításban éles üzemben van (a szolgáltatási szerződés 2026.12.31-én szűnik meg) — 26 év megszakítás nélküli, országos szintű üzem. Szintén Visual Restaurant / Visual Hotel & Restaurant szoftvercsomag (Delphi, később .NET), széles körben használt a magyar vendéglátóiparban.
- **.NET ökoszisztéma (20+ év):** Professzionális C#/.NET fejlesztés, beleértve kötelező hatósági adatszolgáltatási integrációkat (NAV adóhatóság, NTAK turizmus), Akka.NET aktor rendszereket (QCassa/JokerQ: 55+ éles aktor), Avalonia UI cross-platform alkalmazásokat, és Android/iOS telepítést.
- **Magyar Adóügyi Ellenőrző Egység (AEE):** Az eredeti AEE projekt ([48/2013 NGM](https://njt.jog.gov.hu/jogszabaly/2013-48-20-2X)) szoftverének egyedüli fejlesztője. A jelenlegi utód **QCassa/JokerQ** **LMS hash-alapú aláírásokkal (NIST SP 800-208 stateful hash-based signature scheme — kvantum-rezisztens a SHA-256 kollízióellenállása révén)** védett modern helyettesítő, 55+ Akka.NET aktor supervision hierarchiában, szintén egyedül fejlesztve a [8/2025 NGM rendelet](https://njt.jog.gov.hu/jogszabaly/2025-8-20-2X) szerint. Ez a projekt mélyreható, gyakorlati tapasztalatot ad éles aktor-modell architektúrában és hash-alapú aláírás-integrációban, ami közvetlenül befolyásolja a Symphact AuthCode tervezését (aláíró- + bytecode-blacklist ugyanazzal az LMS-családdal).
- **Hardver közös-tervezési tapasztalat:** Párhuzamos fejlesztés a **CLI-CPU / CFPU** nyílt szilícium projekten ([github.com/FenySoft/CLI-CPU](https://github.com/FenySoft/CLI-CPU)) — 250+ xUnit teszt a referencia szimulátoron, 48 CIL-T0 opkód, működő Roslyn-alapú linker, előzetes Verilog RTL (ALU modul: 41/41 cocotb teszt). Ez a hardveres projekt külön NLnet pályázatot folytat (beadva 2026-04-14, ugyanezen a 13. nyílt körön, bírálat alatt), és biztosítja a kétirányú OS↔HW tervezési hurkot.

A Symphact projekt 2026 áprilisában kezdődött. A beadáskor (2026 május) **186 zöld xUnit teszt** fedi le M0.1–M0.4-et és az M0.5 első szeleteit: aktor mag, aktor-közi üzenetküldés, supervision (let-it-crash OneForOne / AllForOne stratégiákkal, lifecycle hookok, hierarchia), a per-aktor parallelizmusú scheduler API (`TInlineScheduler` + `TDedicatedThreadScheduler` CFPU dedikált-core-per-aktor szimulációval), és a BCL-only in-memory event-sourcing journal + snapshot store. Megközelítőleg ~65 óra fókuszált TDD munka szállította ezt az alapot — **ez a velocity dokumentálva van a roadmap-ben** ([`docs/roadmap-hu.md`](roadmap-hu.md)) és alapot ad a mérföldkő-becslésekhez ebben a pályázatban.

**Munkamódszer és transzparencia:** A fejlesztés **LLM-asszisztált TDD ügynök-csapattal** történik (Anthropic Claude Code: Architect + Implementer + Devil's Advocate + Test Guardian + HW Liaison szerepek), ahol a pályázó az architekturális döntéseket, code review-t, integrációs munkát és a TDD red-green-refactor ciklus emberi felügyeletét végzi. Ez magyarázza a roadmap-ben dokumentált velocity-t (pl. M0.4 becsült 24-32 óra, tény ~6 óra emberi órában — `docs/roadmap-hu.md:116`). A pályázott **€36/óra díj az emberi review, integrációs és architekturális döntéshozási órákra szól**; az LLM API-tokenköltség a `Online outreach + dokumentáció` €2,500 tételen belül van elszámolva (~10-15% a teljes költségkeretnek). A 760 órás keret **egyedüli emberi fejlesztő LLM-asszisztált velocityvel teljesíthető 12 hónap alatt**, a múltbéli ~65 óra / 5 hét tempóból extrapolálva (~13 óra/hét). Ez a munkamódszer **explicit megnevezésre kerül a `CONTRIBUTING.md`-ben és a havi research log-ban**, az NLnet transzparencia-elvárás szerint.

---

## Kért összeg

**€30,000**

## Mire megy a kért összeg

> **Megjegyzés a scope-hoz:** Az M0.3 supervision, M0.4 scheduler és az M0.5 perzisztencia BCL-only referencia szeletei (in-memory journal + snapshot store) már kész (2026 május, ~65ó saját finanszírozásból), és **nem** a pályázat finanszírozza. Az alábbi mérföldkövek a hátralévő munkát fedik le egy használható, biztonságos, perzisztens aktor runtime, kernel aktorok, device aktorok és CFPU integrációs demó eléréséhez.
>
> **Megjegyzés a kétirányú hurokról:** A pályázó **mindkét projekt fejlesztője** (Symphact OS és CLI-CPU / CFPU hardver). Ez egyedülálló helyzetet teremt: a kutatási alszeletek eredményei **azonnal beépülhetnek** a CFPU RTL következő iterációjába, nem kell külön egyeztetni független HW-csapattal. A 3–7 várt `osreq-to-cfpu` issue **egyúttal a CLI-CPU projekt belső feature-listájába is bekerül**, és a 2027-es Tiny Tapeout / IHP MPW tape-out cikluson keresztül szilíciumban is megvalósulhat — szintén az NLnet finanszírozott útvonalon (külön pályázat). **Két nyílt projekt, egy fejlesztő, egy nyíltan dokumentált co-design hurok.**

| Mérföldkő | Leírás | Óra | Összeg | Időkeret | Várt `osreq-to-cfpu` issue-k |
|-----------|--------|-----|--------|----------|------------------------------|
| **M1: Persistencia** (roadmap M0.5) | A már leszállított BCL-only `TInMemoryJournal` + `TInMemorySnapshotStore` alapra építve (2026 május, 44 teszt). Bevezeti a `TCasJournal`-t és a `TCasSnapshotStore`-t: content-addressable storage backend (Git/IPFS-szerű filozófia, fájlnév = SHA-256 hash), append-only blob-okkal és automatikus deduplikációval. BCL-only — semmi külső NuGet függőség, csak `System.IO` + `System.Security.Cryptography`. `IPersistenceProvider` ragasztó-réteg, snapshot + replay integráció az M0.3 supervision lifecycle-jával, `RecoveryCompleted` signal, üzenet-stash replay alatt. ~30+ új xUnit teszt. | ~80ó | €2,900 | 1-3. hónap | 1-2 issue (pl. mailbox FIFO mélység, SHA-256 hash utasítás) |
| **M2: Remoting + Capability Registry** (roadmap M0.6 + M2.5) | TCP transport (`ITransport` / `TTcpTransport`), serializáció, location-transparent `Send` routing, CST-alapú capability registry delegation + revocation támogatással. ~80+ új teszt. | ~160ó | €5,800 | 2-7. hónap | 1-2 issue (CST gyorsítótár, capability invalidation broadcast) |
| **M3: Kernel + Device aktorok** (roadmap M2.1, M2.3, M3.1-M3.4) | `TRootSupervisor` boot szekvencia, `TRouter` aktor, device aktor framework + `uart_device`, `gpio_device`, `timer_device` szimulált MMIO-n. (Hot code loading kifejezetten elhalasztva követő pályázatra.) ~80+ új teszt. | ~180ó | €6,500 | 5-10. hónap | 1-2 issue (fault notification, MMIO mapping discipline) |
| **M4: CFPU integrációs demó** (roadmap M0.7 részleges) | End-to-end demó: elosztott számláló aktor szimulált core-okon keresztül a `FenySoft.CilCpu.Sim` NuGet (CLI-CPU C# referencia szimulátor) segítségével. 3-5 konkrét HW követelmény felfedezése, `osreq-to-cfpu` issue-ként iktatva. | ~80ó | €2,900 | 8-11. hónap | 1 issue (SRAM-to-SRAM DMA, vagy explicit „nem kell" konklúzió) |
| **M5: Developer experience + dokumentáció + outreach** | NuGet csomag publikáció — **4 csomag** v0.5-ös érettségi szintnek megfelelő modularizálással: `Symphact.Core` (runtime mag), `Symphact.Platform.DotNet` (.NET host platform), `Symphact.Persistence` (CAS journal + snapshot store), `Symphact.Remoting` (TCP transport + capability registry). A `Symphact.Security` és `Symphact.Devices` szándékosan **NEM** külön csomag v0.5-ben — a capability registry a Core / Remoting között megosztott, a device aktorok pedig a Core-on belül vagy a sample-ek között maradnak. Az aprózás követő milestone-ra (M1.x+) halasztva, amikor van indok rá. Plusz `symphact` CLI eszköz, `symphact` CLI eszköz, kétnyelvű architektúra-dokumentáció, contribution guide, **3 technikai blogposzt** (M1 perzisztencia / M2 capability registry / M3-M4 unified primitive demó), **2-3 online technikai bemutató videó** (M1 / M2 / M3-M4 milestone-okhoz, ~10-15 perces, YouTube vagy hasonló platform — utazás-mentes, aszinkron, hosszabb élettartam), nyilvános GitHub Pages dokumentáció + havi research log. **Outreach-passzív cél a 12. hónapra:** 100+ GitHub stars, 5+ érdemi issue / discussion külső résztvevőtől, dokumentált kapcsolatfelvétel ≥10 akadémiai / ipari kontaktponttal (kontakt-lista a research log-ban). **Aktív kohorta-építés ESZKÖZE, NEM CÉLJA** ebben a fázisban: a fizikai szilícium-demó (CLI-CPU F4 FPGA + F5 Tiny Tapeout, 2027–2028) megléte előtt az ipari / akadémiai LoI-ek **nem reálisak** — a tényleges kohorta-építés a **follow-up pályázat központi deliverable-ja**, amikor van fizikailag mutatható HW. | ~180ó | €6,500 | Folyamatos | — (összesítő dokumentum + research log) |
| **M6: TLA+ specifikáció a capability + supervision invariánsokra — audit-input előkészítés** (roadmap M5.4 részleges) | Initial formális specifikáció a `send` / `receive` szemantikára **TLA+ nyelven** (kiválasztva a Dafny helyett: protokoll-szintű spec, dokumentált iparági precedens AWS/Cosmos DB/Azure-nél, közelebb a Symphact üzenetküldési invariánsokhoz mint a kód-szintű refinement proof). A capability-modell, a supervision lifecycle és a `TCapabilityRegistry` invariánsainak gépileg ellenőrizhető formalizálása (TLC model checker futtatás). **Alapozó munka egy későbbi (érettebb projektszintnél) független biztonsági auditra** — egy v0.5-ös induló projekt esetén a külső audit túlzás; most csak az audit-input bizonyítékainak előkészítése történik. Deliverable: ~3-5 TLA+ modul (Capability.tla, Supervision.tla, Send.tla), TLC model checker konfiguráció, dokumentált invariánsok. | ~80ó | €2,900 | 9-12. hónap | 0-1 issue (verifikációs primitívek) |
| **Mérnöki órák összesen** | | **~760ó** | **€27,500** | **12 hónap** | **3-7 konkrét HW követelmény** |
| **Online outreach + dokumentáció** (lásd Költségstruktúra) | Videó-production, GitHub Pages hosting, technikai dokumentáció kibővítése — **utazás-mentes** | — | €2,500 | Folyamatos | — |
| **Pályázott összeg** | | | **€30,000** | | |

**Költségstruktúra:**
- **Emberi mérnöki órák:** ~760 óra részmunkaidő × €36/óra ≈ €27,500 (architekturális döntések, code review, integráció, TDD red-green-refactor felügyelet — LLM-asszisztált munkamódszer, lásd a fenti „Munkamódszer és transzparencia" szekciót; konzisztens a párhuzamos CLI-CPU pályázat óradíjával; ~63ó/hónap részmunkaidő, illeszkedik az M0.1–M0.4 tényleges velocity-hez)
- **Online outreach + dokumentáció + LLM API-tokenköltség (utazás-mentes):** €2,500 — videó-production, GitHub Pages hosting, technikai dokumentáció kibővítése, **és az ügynök-csapatos fejlesztés Anthropic Claude API-tokenköltsége** (becsült ~€1,500-2,000 a 12 hónap során, a maradék videó-vágás / audio-felszerelés / domain-hosting)
- **Nincs külső biztonsági audit alvállalkozói** — egy v0.5-ös induló projekt esetén ez túlzás; a formális spec az M6-ban audit-előkészítő alap
- **Nincs konferencia-utazás** — a párhuzamos CLI-CPU projekttel együtt ez kapacitás-túlterhelés lenne; online videó-formátum lép a helyébe
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

1. **Követő NLnet pályázat (M5+ deliverable, 12–14. hónap):** A jelen 12 hónap után a Symphact a következő NGI Zero / NGI TALER / NGI Core körre adható be — a központi deliverable a **kohorta-építés** (akadémiai LoI-k, fizetett contributor-onboarding) és az **M3+ multi-agent AI / neuromorf demók kibővítése**, immár a CLI-CPU F3-F4 fázisai mellett (Tiny Tapeout szilícium + FPGA multi-core, várható 2027–2028). Ez a **legreálisabb fenntarthatósági útvonal**, mert a CLI-CPU NLnet pályázat 18 hónapja is folyik, és a két projekt **természetes egymásra-építkezést** mutat.

2. **FenySoft Kft. társfinanszírozás (folyamatos, 2026-tól):** A pályázó FenySoft Kft.-je **éles QCassa/JokerQ-bevétellel** rendelkezik (NGM 8/2025 rendelet által szabályozott pénztárgép-szoftver), ami **kereszt-finanszírozást** biztosít a Symphact karbantartásához a grant utáni időszakban. Konkrét vállalás: **havi ~5 óra saját munka** a Symphact-ra a grant lejárta után is, minimum 12 hónapig — ez biztosítja a CI/CD-zöldet, a NuGet csomag karbantartását, és a havi research log folytatását.

3. **Lehetséges, de NEM ígért utak (csak ha a feltételek megérnek):** (a) `.NET Foundation project membership` — pályázható, ha 1+ év demonstrált projekt + min. 5 contributor (jelen 12 hónapban valószínűtlen, 24+ hónapnál lehetséges); (b) `GitHub Sponsors / Open Collective` — a multi-agent AI piaci ablakra való demó esetén $50–200/hónap reálisnak látszik; (c) kereskedelmi tanácsadás `Akka.NET → Symphact migráció` szabályozott iparágaknak — csak az M3+ demó után. **Ezeket NEM ígérjük, csak megemlítjük lehetőségként.**

**Időbeli ütemezés a párhuzamos CLI-CPU pályázattal és a futó FenySoft Kft. tevékenységekkel:**

| Időszak | CLI-CPU fő-fókusz | Symphact fő-fókusz | FenySoft Kft. ops | Összesen (hét/óra) |
|---------|-------------------|---------------------|-------------------|--------------------|
| **2026 Q3 (1-3. hó)** | F2 RTL bringup (~40ó/hó) | M1 perzisztencia + M5 setup (~25ó/hó) | Atlasz wind-down + QCassa ops (~35ó/hó) | ~25 ó/hét |
| **2026 Q4 (4-6. hó)** | F2 → F3 átmenet (~35ó/hó) | M2 remoting + capability (~35ó/hó) | Atlasz zárás (2026-12-31) + QCassa ops (~30ó/hó) | ~23 ó/hét |
| **2027 Q1 (7-9. hó)** | F3 tape-out (~25ó/hó) | M3 device aktorok mély munka (~55ó/hó) | QCassa/JokerQ ops (~20ó/hó) | ~23 ó/hét |
| **2027 Q2 (10-12. hó)** | F4 FPGA bringup (~25ó/hó) | M4 demó + M6 TLA+ + záró (~50ó/hó) | QCassa/JokerQ ops (~20ó/hó) | ~22 ó/hét |

**Heti átlag a teljes 12 hónapra: ~23 óra mindkét grant + FenySoft Kft. ops kombinálva.** Ez **az LLM-asszisztált velocity mellett sustainable rendszeres heti 30-35 óra mellett** (a múltbéli ~13ó/hét tempóból konzervatív 2× skálázás), és **a Symphact deliverable-ek a 2027-es kalendárban a hangsúlyosabbak**, amikor az Atlasz-szerződés (2026.12.31) már zárult és a CLI-CPU is csendesebb F3/F4 átmeneti fázisban van.

**Tudatos kapacitás-elhatárolás (őszinte limit-megfogalmazás):** A pályázó **nyíltan közli**, hogy a 12 hónapos NLnet-finanszírozott periódus a párhuzamos CLI-CPU NLnet projekttel együttesen **kapacitásának felső határán** van. Ezért: (a) a 12 hónap végén **nem ígér azonnali aktív bővítést** — a Symphact karbantartása csökkentett tempóra (havi ~5–10 óra) áll vissza; (b) a **follow-up NLnet pályázat** természetes folytatás 12–18. hónap között, **csak ha a CLI-CPU F3-F4 fázisai** is haladtak; (c) a **FenySoft Kft. társfinanszírozás** biztosítja a minimum-karbantartást (havi ~5 óra), függetlenül attól, hogy a follow-up pályázat sikeres-e. **Ez tudatos kapacitás-elhatárolás**, nem hiányosság: a 12 hónapos deliverable-ek **konzervatívan vannak méretezve**, hogy ténylegesen teljesüljenek.

A projekt **biztosított fenntartás minimuma**: követő NLnet pályázat + FenySoft Kft. társfinanszírozás. Ez **2–3 év nyugodt karbantartást** ad a grant utáni időszakra, függetlenül attól, hogy a spekulatív utak megvalósulnak-e.

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
| **Anthropic Agent SDK / OpenAI Agents / LangGraph** | Multi-agent koordinációs framework Python-ban | Function-calling + JSON + JIT-eval; nincs capability-szintű izoláció; audit eset-eset alapú | Capability-vel korlátozott aktorok, runtime + HW-szintű validáció, supervision-alapú audit, EU AI Act-konform tool-szintű audit-substrate (nem tanúsított rendszer) |
| **Tock** | Beágyazott OS Rust-ban | Beágyazott fókusz, nem aktor-alapú | Aktor modell, általános célú |

**Egyetlen létező projekt sem kombinálja:** capability-alapú biztonság + aktor runtime + .NET ökoszisztéma + nyílt forráskód + nyílt szilíciummal közös-tervezve. A Symphact egy új pozíció ebben a térben.

---

## Nyitott kutatási kérdések

A Symphact négy bizonyított ötletet (aktor modell, capability biztonság, típus-biztos runtime, determinisztikus üzenetküldés) **olyan hardveres környezetre alkalmaz, amilyen eddig nem létezett**: dedikált core minden aktorhoz, privát SRAM, HW mailbox FIFO-k. Ezért több területen **nincs előzményünk**, amire támaszkodhatnánk — a 12 hónap ezekre **első, mért válaszokat** ad, nem végleges optimumot.

1. **Ütemezés 1000+ aktorra véges core-számon.** Egy aktor = egy OS thread modell 1000 aktorig skálázódik. CFPU-n viszont 10 000+ aktor lehet egy chipen, miközben fizikailag 256–1024 core áll rendelkezésre. **Nyitott kérdés:** hibrid stratégia (statikus pinning + dinamikus work-stealing) vs. compile-time profile-guided allokáció. **Mérés:** throughput és tail latency 100–10 000 aktor között.
   **→ Potenciális HW visszahatás:** ha a hibrid stratégia HW-támogatást igényel (pl. olcsó context-switch dedikált core-on belül), `osreq-to-cfpu` issue → mikroarchitektúra adaptáció.

2. **Backpressure dedikált core-okon.** Ha egy gyártó aktor 10× gyorsabban termel, mint a fogyasztó, a HW mailbox FIFO megtelik. Az Erlang unbounded mailbox-szal él (memory exhaustion kockázat); az Akka backpressure-streamekkel. **Nyitott kérdés:** mi a helyes szemantika capability-alapú modellben? Drop, block-back, adaptive throttling — melyik kompatibilis a let-it-crash filozófiával?
   **→ Potenciális HW visszahatás:** HW-szintű mailbox flow control bit / „mailbox full" interrupt → CFPU mailbox kontrollerbe.

3. **Routing és capability resolution időzítése.** A `TActorRef.SlotIndex` opaque token, a CST mappel HW mailbox címre. **Nyitott kérdés:** a mappelés spawn-time, send-time vagy lazy? Migrálható-e az aktor másik core-ra futás közben anélkül, hogy a `TActorRef` invalidálódna? Egyszerre teljesítmény és biztonság kérdés.
   **→ Potenciális HW visszahatás:** CST gyorsítótár vagy lookup-asszisztencia HW-szinten.

4. **Share-nothing load balancing.** A klasszikus SMP work-stealing feltételezi, hogy az állapot olcsón mozgatható. Privát SRAM-ban tárolt aktor állapot migrációja drága. **Nyitott kérdés:** statikus elosztás profile alapján vs. dinamikus migráció vs. „cold actor pool". **Mérés:** load imbalance arány különböző stratégiákkal 4–256 szimulált core-on.
   **→ Potenciális HW visszahatás:** SRAM-to-SRAM DMA csatorna aktor-állapot migrációhoz, vagy „cold actor pool" HW támogatás.

5. **Supervision latency cross-core.** A supervisor másik core-on van. Ha lehal a gyermek, milyen útvonalon (HW interrupt, mailbox, watchdog) értesül, és milyen latency-vel? **Nyitott kérdés:** a let-it-crash semantika SMP-n triviális, dedikált core-on új tervezést igényel.
   **→ Potenciális HW visszahatás:** dedikált fault notification vonal supervisor-gyermek párokhoz, vagy „shadow mailbox" hierarchia.

6. **Determinizmus vs. paralelizmus.** A formális verifikáció (TLA+) determinisztikus ütemezést igényel; a teljesítmény paralelizmust. **Nyitott kérdés:** ugyanaz a kódbázis tudja-e mindkét módot szolgáltatni csak konfigurációként? Replay-determinizmus event sourcinggal?
   **→ Potenciális HW visszahatás:** opcionális globális ütemezési óra vagy szinkronizációs primitív verifikációs futtatásokhoz.

7. **Memória stratégia heterogén aktor-méretekre.** A .NET GC global. CFPU-n per-core SRAM véges. **Nyitott kérdés:** spill DDR5-be, aktor-szintű generációs GC, vagy compile-time méretkorlát? Ez egyszerre runtime és nyelvi típusrendszer kérdés.
   **→ Potenciális HW visszahatás:** spill mechanizmus a privát SRAM és a megosztott DDR5 között HW segédlettel, vagy aktor-méret limit fordítási időben.

8. **Content-addressed event sourcing capability-alapú aktor rendszerben.** A `TCasJournal` Git/IPFS-szerű content-addressing-et alkalmaz az event journal-ra (fájlnév = SHA-256 hash a blob tartalmából, automatikus deduplikáció, immutable by construction). **Nyitott kérdés:** milyen integritási és teljesítmény-tulajdonságok lépnek fel, és hogyan illeszkedik a hash-ellenőrzés a supervision restart útvonalába a `RecoveryCompleted` signal előtt?
   **→ Potenciális HW visszahatás:** SHA-256 / BLAKE3 hash utasítás CFPU-n (ARM SHA extensions és Intel SHA-NI dokumentált precedensével).

9. **Az egyesítő tézis igazolása.** A pályázat központi tézise, hogy **ugyanaz a `TActor<TState>` alapegység** szolgál klasszikus OS-folyamatként, hardvermeghajtóként, AI-ágens runtime-ként és neuromorf-rokon „okos neuron"-ként — egyazon homogén magrácson. A `TNeuronActor` szerep konkrét műszaki specifikációja a `docs/vision-hu.md`-ben szerepel: **LIF (Leaky Integrate-and-Fire) és Izhikevich neuron-modellek**, ahol a membránpotenciál az aktor állapota, a bejövő/kimenő spike-ok pedig `SpikeMsg(weight: int)` típusú üzenetek; a `TActorRef`-ek a szinaptikus kapcsolatokat reprezentálják. **Nyitott kérdés:** mérhetően működik-e ez a homogenitás a gyakorlatban, vagy a négy szerep olyan különböző optimalizációt igényel, hogy szétdivergálnak? Mi az egyesítés ára teljesítményben, és mi az ára a heterogenitásnak komplexitásban? **Mérés:** négy referenciaaktor (`TDeviceActor`, `LifNeuronActor`, `TAgentActor`, klasszikus `TCounterActor`) közös benchmarkon, késleltetés / áteresztőképesség / kódméret / állapotméret dimenziókban összehasonlítva (a workload-spektrum eltérése — µs UART vs. spike vs. másodperces LLM — explicit dokumentálva).
   **→ Potenciális HW-visszahatás:** ha az egyesítő alapegység HW-szintű kompromisszumot igényel (pl. általánosított, nem szegmens-specifikus mailbox-méret), `osreq-to-cfpu` issue → a CFPU mailbox-tervezésének egységesítése. Ha viszont a négy szerep tényleg divergál, akkor szegmens-specifikus HW-útvonalak — ez **architektúraszintű felfedezés** a paradigma érettségéről.

**A pályázat hozzáállása:** ezekre a kérdésekre **nem ígérünk végleges választ 12 hónapra**. Amit ígérünk: minden mérföldkő mellé egy **kísérleti / mérési alszelet**, ami legalább egy stratégiát mér, dokumentál, és az `osreq-to-cfpu` issue template-en keresztül visszacsatol a CLI-CPU hardver tervezésére. A negatív eredmény (egy stratégia nem működik) is értékes deliverable — egy „ezt nem érdemes silíciumban implementálni" konklúzió önmagában is hasznos a hardveres csapatnak.

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
- **Biztonság-tudatos szektorok:** Egészségügy, kritikus infrastruktúra, autóipar (ISO 26262), orvosi eszközök (IEC 62304), ipari vezérlés (IEC 61508) — ahol a monolitikus kernel formális tanúsítása rendkívül költséges és lassú.
- **CLI-CPU / CFPU hardveres projekt:** A Symphact az elsődleges szoftveres célpont a nyílt szilícium munkához, konkrét HW követelményeket produkálva az `osreq-to-cfpu`-n keresztül.
- **Formális módszerek kutatói közösség:** TLA+ specifikációk a `send`/`receive` szemantikára és a capability-invariánsokra a Symphact-t oktatási és kutatási célponttá teszik (lásd M6).
- **Európai digitális szuverenitás iniciatíva:** Apache-2.0 licensszel, teljesen auditálható, US/ázsiai IP függőségektől mentes.

**Közösségépítési terv:**
- **GitHub repository CI/CD-vel:** Minden commit lefuttatja az összes tesztet; zöld badge látható a README-n.
- **NuGet csomag publikáció:** `Symphact.Core` publikálva a nuget.org-on a 4. hónapra.
- **Dokumentációs weboldal:** GitHub Pages site tutoriállal, API referenciával, tervezési indoklással.
- **Blogposztok:** 3 technikai mérföldkő poszt (M1 / M2 / M3-M4), összhangban az M5 deliverable-lel.
- **Online videó-outreach:** 2-3 online technikai bemutató videó (M1 / M2 / M3-M4 mérföldkövekhez, ~10-15 perces, YouTube vagy hasonló platform) — utazás-mentes, aszinkron, hosszabb élettartam; a konferencia-utazás helyett, a párhuzamos CLI-CPU projekt melletti kapacitás-túlterhelés elkerülésére.
- **`osreq-to-cfpu` issue template:** HW/OS közös-tervezési hozzájárulást tesz lehetővé.
- **Havi progress report** a projekt weboldalán.

**Contributor növekedés vállalás (egyszemélyes pályázói kockázat mitigációja és reális határai):** A Symphact jelenleg egy egy-fejlesztős projekt — ez az NLnet által dokumentált kockázati tényező. Ennek **részleges mitigációja** ebben a 12 hónapban: (a) a teljes munka **nyíltan dokumentált**, **kétnyelvű** (magyar + angol), **TDD-vel tesztelt** — ha a pályázó kiesik, a projekt **átvehető**; (b) `CONTRIBUTORS.md` és governance-dokumentum létrehozása; (c) GitHub Pages-en publikus research log + havi report; (d) `good-first-issue` és `help-wanted` címkézés az onboarding-feladatokon. **Realisztikus passzív outreach-cél a 12. hónapra:** 100+ GitHub stars, 5+ érdemi külső issue/discussion, ≥10 dokumentált kapcsolatfelvétel akadémiai / ipari kontaktponttal. **A tényleges fizetett vagy aktív contributor-bevonás chip-bizonyíték nélkül irreális** — ezért **kifejezetten a follow-up pályázat központi deliverable-ja**, amikor a CLI-CPU F3-F4 fázisai (Tiny Tapeout szilícium + FPGA multi-core) biztosítják a vonzerőt.

**Miért „kohorta", miért nem „közösség"?** A paradigmaváltási út egyetlen ember által nem járható végig (lásd a hosszú távú vízió szekciót). A „kohorta" itt a hosszú távú kutatási / mérnöki úton tovább lépni tudó core team-et jelöli (governance-dokumentum, utódlási tervezés, `CONTRIBUTORS.md` core team) — nem a felhasználói közösséget. Ez a vállalás tehát **fenntarthatósági biztosíték**, nem outreach-mutató.

**Nem-célok (explicit scope-határok ebben a 12 hónapban — a hosszú távú vízió külön szekcióban):**
- **NEM kész Linux-helyettesítő.** A hosszú távú vízió a post-Dennard paradigma elérését 15–30 éves útnak látja. Ez a pályázat ennek az útnak a **mérnöki alapját** rakja le, nem a végcélt szállítja.
- **NEM bare-metal hardverkernel ebben a pályázati időszakban.** .NET-hoszt OS-en fut; a CFPU-natív végrehajtás követő fázis (várható tape-out 2028+).
- **NEM termelési több-csomópontos elosztott rendszer.** A helyfüggetlen címfeloldási primitívek M2-ben landolnak (TCP-transport, capability-registry), de a több-csomópontos termelési telepítés a 3. fázis (követő pályázat).
- **NEM AGI vagy frontier AI-modell.** A multi-ágenses AI-infrastruktúra célja, hogy **strukturált runtime**-ot kínáljon a meglévő LLM-eknek (Claude, GPT, Llama) — nem új modell.

---

## Hosszú távú vízió: a post-Dennard paradigma 15–30 éves útja

**A Symphact + CFPU nem inkrementális termék, hanem paradigma-alapozás.** A jelenlegi számítási stack (Linux + GPU + cache-koherencia) **fizikai falba ütközött**, és a következő 15–30 évben kényszerűen át fog alakulni. A Symphact ennek az átmenetnek az **első nyíltan dokumentált mérnöki rétege**.

**A történelmi precedens, ahová illik:** Unix 1969-ben (kortárs konszenzus: a Multics elég), aktormodell 1973-ban (kortárs konszenzus: tisztán elméleti), Linux 1991-ben (kortárs konszenzus: játék, nem komoly OS), iPhone 2007-ben (kortárs konszenzus: PDA + telefon = niche). **Minden paradigmaváltás víziós úttörővel kezdődik, akit a kortárs szakértői konszenzus túl ambiciózusnak tart.** A jelenlegi pillanat ennek a sorozatnak a következő iterációja, és a paradigmaváltás birtoklása **a 21. század második felének számítási dominanciáját jelenti**.

**Útszakaszok (becslés, nem ígéret):**

| Évek | Mérföldkő | Kohorta |
|---|---|---|
| **2026** | M0.5–M3.2 (jelen pályázat) | Egyetlen pályázó + 3+ közreműködő |
| **2027–2028** | M4–M6 + multi-ágenses AI piaci pozíció + első CFPU Tiny Tapeout | Symphact-közösség magja (10–20 fő) |
| **2028–2032** | Üzemi szintű multi-ágenses runtime + IHP MPW több-komponensű CFPU + akadémiai validáció (TLA+, formális spec) | Alapító kohorta (50–100 fő) + akadémiai partnerek |
| **2032–2040** | Niche-üzemi telepítés (autóipar, orvosi, kritikus infrastruktúra) + általános célú multi-ágenses AI-infrastruktúra | Európai konzorcium, EU Horizon-szintű finanszírozás |
| **2040+** | A post-Dennard paradigma széles körű elterjedése, „Linux a 70-es évekhez képest" pozíció | Globális közösség |

**Az alapító feladata** (a jelen pályázat kedvezményezettje) **nem az út végigjárása**, hanem **az alap lerakása és a kohorta átadása**. Neumann János nem érte meg a modern számítógépeket; Hewitt nem érte meg a kereskedelmi aktor-rendszereket. **Az alapító szerepe az, hogy bizonyítsa az út járhatóságát, és felépítse a kohortát, amelyik tovább viszi.**

**Mit kell teljesíteni a 12 hónap végére**, hogy az út járható legyen:
1. **Bizonyítva:** az egyesítő tézis működik — ugyanaz a `TActor` szolgál klasszikus, hardver- és AI-szerepben (M3 demó)
2. **Dokumentálva:** a 9 kutatási kérdés első mért válaszai + 3–7 konkrét HW-követelmény (M1–M6)
3. **Bevonva:** 3+ lényegi külső közreműködő + 2+ akadémiai/ipari támogató levél (M5 kohorta-építés)
4. **Publikálva:** 3 blogposzt + 2-3 online technikai bemutató videó + nyílt research log (M5 outreach)
5. **Folytatható:** governance-dokumentum + `CONTRIBUTORS.md` core team + utódlási tervezés (M5)

Ha ezek megvannak, a **paradigmaváltási út járható** — a pályázó személyétől függetlenül.

---

## .NET függetlenség és szabvány illesztés

A CIL specifikáció (ECMA-335) egy ISO/IEC 23271 által ratifikált nemzetközi szabvány. A Symphact a bytecode formátumot célozza, nem valamely saját Microsoft runtime-ot. Alternatív CIL implementációk léteznek (Mono, örökölt .NET Framework compilerek, különböző Roslyn-független front-endek). A runtime design CIL szinten működik, és független bármely upstream runtime változástól.

**Apache-2.0 licensz** permisszív felhasználást biztosít bármely downstream projektben, beleértve a kereskedelmi felhasználást — konzisztens a .NET ökoszisztéma normáival és a .NET Foundation projekt elvárásokkal.

---

## Melléklet terv

PDF mellékletek (~15-20 oldal összesen):
1. **Architektúra áttekintés** — kivonat a `docs/vision-en.md`-ből (capability modell, aktor hierarchia, üzenet routing)
2. **Roadmap** — `docs/roadmap-hu.md` Fázis 1-7 óra becslésekkel
3. **Jelenlegi állapot snapshot** — 186 xUnit teszt output (M0.1–M0.4 ✅, M0.5 BCL-only szeletek ✅), kód metrikák, repo screenshot
4. **CLI-CPU ↔ Symphact interakciós diagram** — OS követelmények visszacsatolási hurok
5. **Threat model összefoglaló** — capability hamisítás, supervisor escape, hot-load tampering
6. **1-oldalas executive summary** — probléma, megközelítés, deliverable-ök, költségvetés, fenntarthatóság
