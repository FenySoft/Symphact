# Symphact — halasztott taszkok audit-fájl

> Az itt felsorolt elemek **explicit, dokumentált scope-döntések** vagy
> **megvalósított szerződés-magyarázatok** — egyik sem rejtett kódbeli akna.
> A `commit-deferred-audit` hook az override magic-stringhez ennek a fájlnak
> a meglétét várja el. Minden bejegyzés visszamutat arra a dokumentumra,
> ahol a halasztás indoklása megtalálható.

## Aktív halasztott elemek

### 1. NLnet draft — EU AI Act compliance "later stage"

- **Hivatkozás:** `docs/nlnet-application-draft-en.md` (Abstract / Multi-agent
  AI infrastructure bekezdés), `docs/nlnet-application-draft-hu.md`
- **Tartalom:** "actual certification compliance only becomes meaningful at a
  later, more mature project stage — at the 12-month mark Symphact delivers
  an audit-input substrate (structured event log + capability graph), not a
  certified system."
- **Indoklás:** v0.5 startup-szakaszban az EU AI Act high-risk compliance
  certification idő előtti — az NLnet 12 hónap **audit-input szubsztrátot**
  szállít, nem certified rendszert. A certification follow-up grant feladata.
- **Felülvizsgálat:** follow-up NLnet pályázat M2+ szakasza (2027 H2)

### 2. NLnet draft — M5 NuGet csomag-bontás "deferred to M1.x+"

- **Hivatkozás:** `docs/nlnet-application-draft-en.md` (M5 sor), `docs/attachments/02-roadmap.md`
- **Tartalom:** "`Symphact.Security` and `Symphact.Devices` are deliberately
  not separate packages at v0.5 — the capability registry is shared between
  Core / Remoting, and device actors stay inside Core or in samples. Further
  breakdown deferred to a later milestone (M1.x+) when there is a concrete
  reason."
- **Indoklás:** v0.5-ben a capability registry közös a Core / Remoting között,
  a device aktorok a Core-ban vagy samples-ben élnek. A 4 csomag (Core,
  Platform.DotNet, Persistence, Remoting) megfelel a jelenlegi érettségnek;
  túlbontás nélküli, valós igényre reagáló bontás M1.x+-ben.
- **Felülvizsgálat:** M1.x milestone tervezése — amikor konkrét adoption- vagy
  függőség-szétválasztási igény indokolja

### 3. NLnet draft — M6 független biztonsági audit "later stage"

- **Hivatkozás:** `docs/nlnet-application-draft-en.md` (M6 sor)
- **Tartalom:** "Groundwork for a later (at maturer project stage) independent
  security audit — at v0.5 startup stage external audit is premature; what
  happens here is only the preparation of audit-input evidence."
- **Indoklás:** v0.5-ben a külső biztonsági audit idő előtti. Az M6 a TLA+
  spec + TLC model-checker futtatásokkal **audit-input bizonyítékot** készít
  elő (Capability.tla, Supervision.tla, Send.tla) — maga az audit csak
  érettebb projekt-fázisban értelmes (follow-up grant scope).
- **Felülvizsgálat:** follow-up NLnet pályázat M3+ (2027 vagy később)

### 4. Scheduler-szerződés — pre-Attach Signal deferred replay

- **Hivatkozás:** `tests/Symphact.Core.Tests/TInlineSchedulerTests.cs` —
  `Signal_BeforeAttach_DefersUntilAttach` teszt
- **Tartalom:** "the wakeup is expected to be deferred and replayed at the
  first Quiesce after Attach"
- **Indoklás:** ez NEM halasztás, hanem a **megvalósított runtime-szerződés
  leírása**: a Signal Attach előtt is elfogadott; a wakeup belső queue-ban
  marad, és az Attach utáni első Quiesce-nál replay-elődik. A `TInlineScheduler`
  ezt implementálja, a teszt ezt verifikálja. Nincs benne TODO/FIXME, a
  viselkedés stabil és tesztelt.
- **Felülvizsgálat:** —  (lezárt, stabil viselkedés)
