# Biztonsági szabályzat

> English version: [SECURITY.md](SECURITY.md)

A Symphact egy capability-alapú aktor runtime, amelynek fő értékajánlata az izoláció és a nem
hamisítható referenciák. A biztonsági bejelentéseket komolyan vesszük — beleértve a runtime, a
perzisztencia-réteg, az ütemező és a supervision-modell hibáit is.

## Támogatott verziók

A Symphact a **v0.5 (pre-alfa)** állapotban van. A biztonsági javítások a `main` ágra kerülnek.
Egyelőre nincs kiadott vagy visszaportolt verzió; amíg nincs stabil kiadás, csak a `main`
támogatott.

| Verzió | Támogatott |
|--------|------------|
| `main` (0.5.x pre-alfa) | ✅ |
| régebbi commitok | ❌ |

## Sebezhetőség bejelentése

**Kérjük, sebezhetőséghez NE nyiss publikus issue-t.**

Jelentsd privát csatornán, GitHubon keresztül:

1. Nyisd meg a repó **Security** fülét → **Report a vulnerability**
   (<https://github.com/FenySoft/Symphact/security/advisories/new>).
2. Írd le a hibát, az érintett komponenst és a reprodukciót (ideális esetben egy bukó teszt).

Ha a GitHub privát jelentés nem elérhető számodra, vedd fel a kapcsolatot a maintainerrel privátban
a GitHub-profilján: [@Hocza-Jozsef-Szabolcs](https://github.com/Hocza-Jozsef-Szabolcs).

## Mit tartalmazzon a bejelentés

- Érintett komponens (`Symphact.Core`, `Symphact.Persistence`, ütemező, supervision, …)
- Symphact commit / verzió és .NET SDK verzió
- Minimális reprodukció — egy piros xUnit teszt ideális (a projekt szigorúan teszt-vezérelt)
- Hatásbecslés: melyik izolációs vagy capability-invariáns sérül?

## A mi vállalásunk

- Igyekszünk **5 munkanapon** belül visszaigazolni a bejelentést.
- Folyamatosan tájékoztatunk a javításról, és egyeztetjük a nyilvánosságra hozatal ütemezését.
- Engedélyeddel feltüntetünk az advisory-ban és a kiadási jegyzetekben.

## Hatókör-megjegyzés

A Symphact a [CFPU hardverrel](https://github.com/FenySoft/CLI-CPU) együtt tervezett; egyes
izolációs tulajdonságok a jövőben hardveresen kikényszerítettek lesznek. A v0.5-ben a runtime
kizárólag **szoftveresen** kényszerített izolációt nyújt — kérjük, a bejelentést a jelenlegi
szoftveres fenyegetés-modellhez igazítsd (lásd [`docs/trust-model-hu.md`](docs/trust-model-hu.md)).
