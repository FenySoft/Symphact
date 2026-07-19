---
status: archived
---

# NLnet Pályázat — Beadás utáni korrekciók

> **Cél:** Ez a dokumentum a Symphact NLnet NGI Zero Commons Fund pályázat **beadása UTÁN** azonosított belső review korrekciókat rögzíti. A beadott fájl ([`nlnet-application-draft-hu.md`](nlnet-application-draft-hu.md) ill. `-en.md`, valamint az [`attachments/`](attachments/) mellékletek) **változatlanul** megőrzi, mint az NLnet-hez beérkezett szöveg hiteles rekordját.
>
> **Hogyan használjuk:**
> - Ha az NLnet bírálók pontosítást kérnek, használd az alábbi finomított szövegeket a válaszhoz.
> - A follow-up pályázat már ezeket a korrekciókat kezdetektől beépíti.
> - A párhuzamos CLI-CPU pályázat ([`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU)) hasonló formátumú korrekciós dokumentummal rendelkezik: [`nlnet-corrections-hu.md`](https://github.com/FenySoft/CLI-CPU/blob/main/docs/nlnet-corrections-hu.md).

> English version: [nlnet-corrections-en.md](nlnet-corrections-en.md)

> Version: 1.0 — 2026-07-18

---

## 1. korrekció — AllForOne stratégia beadáskor túlállítva

**Beadott szöveg (draft, 37. és 98. sor; hasonlóan `attachments/01-architecture-overview.md` 34. sor):**
> „A projekt befejezte az M0.1–M0.4 mérföldköveket és félúton jár az M0.5-ben: **186 zöld xUnit teszt** fedi le a core primitíveket — … supervision (M0.3 — `ISupervisorStrategy`, **OneForOne / AllForOne**, lifecycle hookok, aktor hierarchia) … Az M0.3 supervision, M0.4 scheduler és az M0.5 perzisztencia BCL-only referencia szeletei a v0.1 (2026 április) és a beadás között elkészültek…"

**Probléma:** A beadott szöveg azt állítja, hogy az M0.3 supervision — beleértve az `TAllForOneStrategy`-t — a beadás **előtt** teljesen elkészült. A git történet szerint ez nem igaz: a `TAllForOneStrategy` implementáció ténylegesen a **2026-06-30-i `d076f5a` commitban** ("feat: TAllForOneStrategy implementáció + 12 új teszt (#25)") készült el — ez a beadási határidő (2026-06-01) **után** van. A beadáskor csak a `TOneForOneStrategy` volt kész; a beadott "186 teszt" szám és az "OneForOne / AllForOne" leírás egyaránt a beadás utáni állapotot tükrözi, nem a beadáskorit. A `03-status-snapshot.md` melléklet forrásfájl-leltára ezt implicit alá is támasztja (csak a OneForOne fájlt listázza).

**Finomított szöveg:**
> A beadáskor (2026-06-01) a `TOneForOneStrategy` (child-szintű újraindítás) volt kész, 174 zöld xUnit teszttel. A testvér-stratégia, `TAllForOneStrategy` (csoport-szintű újraindítás, +12 teszt) a beadás után, **2026-06-30-án** készült el önfinanszírozott munkával — ez is a grant-scope-on kívüli, de a beadáskor még nem volt leszállítva. A jelenlegi (2026-07-18) állapot: mindkét stratégia kész, összesen 198 zöld xUnit teszt.

---

## 2. korrekció — Tesztszám elavult + a kísérő README milestone-táblája nem áll össze

**Beadott szöveg (`attachments/03-status-snapshot.md`, „Test suite" szekció):**
> „## 2. Test suite — 186 green xUnit tests … | `Symphact.Core.Tests` | **120** | … | `Symphact.Platform.DotNet.Tests` | **22** | … | `Symphact.Persistence.Tests` | **44** | … | **Total** | **186** |"

**Probléma:** A melléklet saját belső aritmetikája (120+22+44=186) helyes és a beadás pillanatában pontos volt. Két dolog viszont árnyalja:
1. A szám **azóta elavult**: a mai (2026-07-18) tényleges `dotnet test` eredmény **198** (132 Core + 22 Platform.DotNet + 44 Persistence) — a Core-szám AllForOne-nal nőtt 120→132-re (lásd 1. korrekció).
2. A repó nyilvános `README-hu.md`/`README.md` fájlja (nem a beadott anyag része, de bárki számára elérhető, aki a beadott szövegből a GitHub-linket követi) a beadás idején egy **mérföldkő-szintű** bontást is közölt (30+16+30+86+44=206), ami **nem egyezett** a projekt-szintű 186-tal — egy alaposabb bíráló ezt a repóban észrevehette volna. Ezt a README-t **időközben (2026-07-18) kijavítottuk**: a fejléc-szám 198-ra frissült, és egy megjegyzés jelzi, hogy a mérföldkő-szintű bontás pontosítás alatt áll.

**Finomított szöveg:**
> A beadáskor (2026-06-01) 174 zöld xUnit teszt volt kész (120 Core + 22 Platform.DotNet + 44 Persistence — a Core-szám a mellékletben tévesen 120-ként szerepel, ami már tartalmazza a OneForOne-t, de nem az AllForOne-t). A jelenlegi (2026-07-18) állapot: **198 zöld xUnit teszt** (132 Core + 22 Platform.DotNet + 44 Persistence).

---

## 3. korrekció — Óra-kettőskönyvelés a roadmap-becslés és a grant-tábla között

**Beadott szöveg (`attachments/02-roadmap.md`, Grant-funded scope tábla):**
> „| **M1: Persistence** | M0.5 (content-addressed) | **~80h** | €2 900 | Months 1-3 |"

**Ezzel egyidejűleg, ugyanannak a projektnek a saját roadmap-becslése (`docs/roadmap-hu.md`, M0.5 szekció, a beadáskor):**
> „**Becsült óra:** ~16-24" (a teljes M0.5 mérföldkőre, BCL-only + content-addressed együtt)

**Probléma:** Ugyanarra a deliverable-re (M0.5 content-addressed szint) a beadott melléklet **~80 órát** allokál a grant-táblában, miközben a projekt saját, ugyanabban az időszakban érvényes roadmap-becslése **~16-24 órát** mond a TELJES M0.5-re (BCL-only rész is beleértve, ami már kész volt). Ez kb. 3,5-5×-ös eltérés, egyeztető magyarázat nélkül a beadott anyagban. Hasonló, kisebb mértékű eltérés a többi grant-mérföldkőnél is megfigyelhető (pl. M2 Remoting+Capability Registry ~160h grant vs roadmap Phase 2 összesen ~120-160h — de a Phase 2 négy további tételt is tartalmaz M2.5-ön kívül).

**Finomított szöveg:**
> A grant-táblában szereplő óraszámok **nem csak a kódolási munkát** fedik, hanem a hozzá kapcsolódó dokumentációt, `osreq-to-cfpu` issue-előkészítést, formális specifikáció-írást, blogposzt/videó-tartalmat és pályázat-adminisztrációt is — ez a bontás a follow-up jelentésben (research log) tételesen dokumentálva lesz. A technikai roadmap-becslés (`docs/roadmap-hu.md`) kizárólag a runtime-kódolási munkát becsüli, ezért szisztematikusan alacsonyabb a grant-tábla teljes-scope óráinál.

---

## 4. korrekció — TLA+ vs „TLA+ vagy Dafny" ellentmondás a mellékletben

**Beadott szöveg (`attachments/02-roadmap.md`, Phase 5 tábla, M5.4 sor):**
> „| M5.4 Formal Verification Foundations | ~32–48 | **TLA+ / Dafny** spec of `send`/`receive` semantics, capability invariants |"

**Ezzel szemben a beadott draft M6 grant-mérföldköve (ugyanabban a beadási csomagban, 121. sor):**
> „Initial formális specifikáció a `send` / `receive` szemantikára **TLA+ nyelven** (kiválasztva a **Dafny helyett**: protokoll-szintű spec, dokumentált iparági precedens AWS/Cosmos DB/Azure-nél, közelebb a Symphact üzenetküldési invariánsokhoz mint a kód-szintű refinement proof)."

**Probléma:** A beadott csomagon belül két dokumentum két különböző állapotot tükröz: a draft M6 sora **explicit, indokolt döntést** rögzít (TLA+, nem Dafny), miközben a `02-roadmap.md` melléklet Phase 5 táblája még mindkettőt nyitva hagyja lehetőségként.

**Finomított szöveg:**
> A formális verifikációs eszköz választása **lezárt döntés: TLA+** (nem Dafny) — indoklás: protokoll-szintű specifikáció jobban illeszkedik az üzenetküldési invariánsokhoz, mint a Dafny kód-szintű refinement-bizonyítási modellje, és van dokumentált iparági precedens (AWS, Cosmos DB, Azure). A `02-roadmap.md` melléklet M5.4 sora a döntés előtti, nyitottabb megfogalmazást tükrözi — a hatályos irány a draft M6 sorában rögzített.

---

## 5. korrekció — F-fázis címkék felcserélve a draft M5 sorában

**Beadott szöveg (draft, 120. sor, M5 mérföldkő leírás):**
> „…a fizikai szilícium-demó (**CLI-CPU F4 FPGA + F5 Tiny Tapeout**, 2027–2028) megléte előtt…"

**Ezzel szemben a beadott csomag TÖBBI helye (draft 65. és 152. sora, valamint az `attachments/04-cli-cpu-symphact-interaction.md` melléklet, „Funded milestones" sor) egybehangzóan:**
> „…az aktornatív szilícium kimenetet a **CFPU F3-F4 fázisai** termelik (várható 2027–2028)…" / „…CLI-CPU **F3-F4 fázisai** mellett (**Tiny Tapeout szilícium + FPGA multi-core**, várható 2027–2028)…" / „**Funded milestones** … F2 RTL, **F3 Tiny Tapeout, F4 FPGA multi-core**"

**Probléma:** A draft M5 sorában (120. sor) a két fázis-címke fel van cserélve — helyesen F3=Tiny Tapeout, F4=FPGA multi-core, a sor viszont fordítva írja ("F4 FPGA + F5 Tiny Tapeout"). Ez elszigetelt elírás, a beadott csomag többi öt helye konzisztensen a helyes sorrendet használja.

**Finomított szöveg:**
> „…a fizikai szilícium-demó (**CLI-CPU F3 Tiny Tapeout + F4 FPGA multi-core**, 2027–2028) megléte előtt…"

---

## 6. korrekció — Sky130 PDK elavult célmegjelölés a mellékletben

**Beadott szöveg (`attachments/04-cli-cpu-symphact-interaction.md`, összehasonlító tábla):**
> „| **Target** | .NET 10 library (Windows / Linux / macOS) | Verilog synthesis, **Sky130 PDK** |"

**Probléma:** A CLI-CPU testvér-projekt a beadás óta pivotált: az F3 első szilícium célja **IHP SG13G2** (2026-06-01-i egységes on-chip SRAM ADR), a hosszabb távú F6.7 pedig **22FDX**-re néz. A Sky130 (SkyWater, US) mint kizárólagos célmegjelölés a beadás idején is csak részben volt pontos (lásd a CLI-CPU saját `nlnet-corrections-hu.md` #2 és #13 korrekcióit, amik ugyanezt a kérdést tisztázzák a hardver-oldali pályázatban), és azóta tovább avult.

**Finomított szöveg:**
> „| **Target** | .NET 10 library (Windows / Linux / macOS) | Verilog synthesis, **IHP SG13G2** (F3), Sky130 (Tiny Tapeout belépő), **22FDX** (F6.7+) |"
>
> Lásd a párhuzamos CLI-CPU pályázat korrekciós dokumentumának #2 és #13 pontját a teljes indoklásért.

---

## 7. korrekció — Draft státusz-mező elavult

**Beadott szöveg (draft fejléc):**
> „**Státusz:** DRAFT — beadás-előkészítés a 2026-06-01-i határidőre"

**Probléma:** A git történet szerint a beadás a 2026-06-01-i határidőre ténylegesen megtörtént (lásd az ezt megelőző, „NLnet beadás-előtti finomítások" jellegű commitokat és a 2026-06-13-i publikus funding-felület tisztítást, ami a beadás utáni állapotot feltételezi). A "DRAFT — beadás-előkészítés" fejléc a beadás óta pontatlan.

**Finomított szöveg:**
> „**Státusz:** BEADVA 2026-06-01, 13. nyílt kör — bírálat alatt."

---

## Mit tegyünk, ha az NLnet bírálók pontosítást kérnek

1. **Ismerjük el, hogy a beadott szöveg a 2026-06-01-i állapotot tükrözi**, és ajánljuk fel ezt a korrekciós dokumentumot pontosításként.
2. **A leginkább lényegi korrekciókkal kezdjünk** (#1 AllForOne-túlállítás, #3 óra-kettőskönyvelés) — ezek érintik a deliverable-hitelesség és a költségvetés-indoklás kérdését.
3. **Hivatkozzunk a párhuzamos CLI-CPU pályázat hasonló korrekciós dokumentumára** (#6-hoz kapcsolódóan is), hogy a két pályázat egymást megerősítő, átlátható képet adjon.
4. **A tesztszámot (#2) minden válaszban a legfrissebb `dotnet test` eredményre** hivatkozva adjuk meg, ne a beadott pillanatképre.

---

## Changelog

| Verzió | Dátum | Összefoglaló |
|--------|-------|-------------|
| 1.0 | 2026-07-18 | Kezdeti beadás utáni korrekciós dokumentum. 7 javítási pont katalogizálva belső review alapján, a CLI-CPU testvér-projekt precedens-formátumát követve. A beadott draft és mellékletek változatlanul megőrizve. |
