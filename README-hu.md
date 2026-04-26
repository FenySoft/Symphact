# Symphact

> **Capability-alapú aktor runtime biztonságos .NET számításhoz — co-designed a Cognitive Fabric Processing Unit (CFPU) hardverrel.**
> Minden entitás aktor. A kommunikáció kizárólag üzenetküldéssel történik. Hardveresen kikényszerített izoláció, formális verifikálhatóság, és közös fejlődés a nyílt szilíciummal.

> English version: [README.md](README.md)

> Verzió: 0.1 (pre-alfa — aktív fejlesztés)

## Mi a Symphact?

A Symphact egy **capability-alapú aktor runtime** .NET-re, egyetlen egyszerű elvre építve:

> Minden állapot-tartó entitás egy aktor. Az aktorok kizárólag immutable üzenetekkel kommunikálnak, mailbox-okon keresztül. Az izoláció **hardveres tulajdonság**, nem szoftveres konvenció.

Ma a Symphact **bármely .NET hoszton** (Windows, Linux, macOS) fut referencia runtime-ként. Holnap natívan fog futni a **Cognitive Fabric Processing Unit (CFPU)** hardveren — egy új kategóriájú feldolgozó egységen, ahol minden core **fizikailag egy aktor**, saját privát SRAM-mal és hardveres mailbox FIFO-kkal.

**A két projektet tudatosan együtt fejlesztjük:** az OS alakítja a hardveres követelményeket, a hardver pedig földbe ereszti az OS tervezési döntéseit. Ez a kétirányú visszacsatolás az oka, hogy az Apple M-chipjei ilyen szoros OS/hardver integrációt érnek el — mi ugyanezt a filozófiát alkalmazzuk nyílt forráskódú stack-en.

## Miért külön repository?

A Symphact három okból kapott önálló repót:

1. **Eltérő fejlesztői közönség** — egy .NET fejlesztőnek ne kelljen Verilog-ot, cocotb-t vagy Yosys scripteket olvasnia ahhoz, hogy aktor runtime-hoz hozzájáruljon
2. **Független életciklus** — a Symphact bármely CIL host-on fut ma; nem blokkol a szilícium elkészültén
3. **Tiszta licensz** — Apache-2.0 (permisszív) illeszkedik a szélesebb .NET ökoszisztémához; a CFPU hardver repo CERN-OHL-S-t használ (strong reciprocal), ami hardver design-okhoz megfelelő

A hardveres co-development szál itt található: [`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU/blob/main/README-hu.md) — a CFPU nyílt forráskódú referencia implementációja.

## Gyors indulás

```bash
git clone https://github.com/FenySoft/Symphact.git
cd Symphact
dotnet build Symphact.sln -c Debug
dotnet test
```

## Tervezési alapelvek

1. **Minden aktor.** Kivétel nélkül. Device driverek, supervisorok, service-ek, üzleti logika — mind aktor.
2. **Nincs shared memory, soha.** A core-ok és aktorok kizárólag immutable üzenetekkel kommunikálnak mailbox-okon át.
3. **Let it crash.** Egy hibázó aktort a supervisor-a újraindítja. A rendszer nem védekezik minden hibára defenzíven.
4. **Supervision hierarchia.** Minden aktornak van supervisor-a. A hiba a fa mentén fölfelé propagál, amíg valaki lekezeli.
5. **Location transparency.** Egy aktor referencia nem árulja el, hogy a célpont lokális, távoli, vagy másik chip-en van.
6. **Capability-alapú biztonság.** Egy aktor csak akkor küldhet üzenetet egy másiknak, ha birtokolja a capability-t (nem hamisítható referencia).
7. **Hot code loading.** Egy futó rendszer képes új kódot fogadni leállás nélkül (Erlang-stílusban).
8. **Determinizmus alapértelmezésben.** Ugyanaz a bemenet, ugyanaz az állapot. Reprodukálható bugok, replay debuggolás, formális verifikáció.

## Projekt állapot

**v0.1 — pre-alfa.** A fejlesztés most indult. Az első deliverable-ök:

- [ ] `IMailbox` + `TMailbox` in-memory implementáció (~8 teszt, TDD)
- [ ] `TActorRef` value type (~5 teszt)
- [ ] `TActor<TState>` + `TActorSystem` a `Spawn` / `Send` / `Receive` primitívekkel (~10 teszt)
- [ ] Első end-to-end demó: `CounterActor`

Teljes roadmap: [`docs/roadmap-hu.md`](docs/roadmap-hu.md).

## Kapcsolat a CLI-CPU-val / CFPU-val

A Symphact **bármely** CIL hoszton fut. A hardveres co-design céljából **kiegészítésként** a Symphact workload-okat a CLI-CPU referencia szimulátoron is futtatjuk (a hamarosan érkező `FenySoft.CilCpu.Sim` NuGet csomagon át), hogy felfedezzük a hardveres követelményeket:

- Mailbox mélység profilozás → CFPU FIFO méretezés
- Kontextus-méret mérés → per-core SRAM budget
- Capability token formátum → router HW szélesség
- Device aktor minták → MMIO absztrakció az első chipen (F3 Tiny Tapeout)

Az OS-ből a hardver felé kommunikált követelményeket a [`osreq-to-cfpu`](.github/ISSUE_TEMPLATE/osreq-for-cfpu.md) issue template és a [`docs/osreq-to-cfpu/`](docs/osreq-to-cfpu/) könyvtár követi nyomon.

## Licenc

Apache License 2.0 — lásd [LICENSE](LICENSE) és [NOTICE](NOTICE).

## Közreműködés

Lásd [CONTRIBUTING-hu.md](CONTRIBUTING-hu.md).

---

## Changelog

| Verzió | Dátum | Összefoglaló |
|--------|-------|-------------|
| 0.1 | 2026-04-16 | Kezdeti repo csontváz. Apache-2.0 licensz, .NET projekt struktúra, első TDD iteráció célja. |
