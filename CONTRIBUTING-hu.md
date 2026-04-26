# Közreműködés a Symphact-ben

> English version: [CONTRIBUTING.md](CONTRIBUTING.md)

Köszönjük az érdeklődést a Symphact iránt!

## Előfeltételek

- **.NET 10.0 SDK** vagy újabb
- **Git**
- Szerkesztő: Visual Studio, VS Code, JetBrains Rider, vagy bármilyen C# 13 támogatású editor

## Build és teszt

```bash
# Teljes build
dotnet build Symphact.sln -c Debug

# Összes teszt futtatása
dotnet test

# Egyetlen teszt futtatása
dotnet test --filter "FullyQualifiedName~TMailboxTests"

# Build restore nélkül (ha már korábban le volt fordítva)
dotnet build Symphact.sln -c Debug --no-restore
```

## Teszt-vezérelt fejlesztés (TDD) — kötelező

Minden új funkció, hibajavítás, refaktor **teszt-elsővel** készül:

1. **Teszt írása** — a kívánt viselkedést egy vagy több xUnit teszttel leírjuk
2. **Futtatás** — a teszt **buknia kell** (piros) — ez igazolja, hogy a teszt valódi
3. **Implementáció** — minimális kód, ami zöldre fordítja
4. **Refaktor** — tisztítás, a tesztek továbbra is zöldek
5. **Commit** — csak zöld tesztekkel

### Szabályok

- ❌ **TILOS** tesztek nélkül commitolni (kivétel: konfig fájlok, dokumentáció)
- ❌ **TILOS** assert nélküli (mindig zöld) tesztet írni (nem teszt)
- ✅ **KÖTELEZŐ** lefedni: aktor logika, mailbox implementáció, capability műveletek, supervisor-ok
- ✅ **KÖTELEZŐ** létrehozni a teszt projektet, ha még nincs
- ✅ Ha meglévő kódhoz kell hozzányúlni és nincs teszt → előbb teszt, aztán módosítás

## Elnevezési konvenciók

Ez a projekt ugyanazokat a konvenciókat követi, mint a [`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU/blob/main/README-hu.md), a konzisztencia érdekében:

| Elem | Prefix | Példa |
|------|--------|-------|
| Osztály, record, enum | `T` | `TActor`, `TMailbox`, `TActorRef` |
| Interfész | `I` | `IMailbox`, `ISupervisor` |
| Metódus paraméter | `A` | `AMessage`, `AActorRef` |
| Privát / protected mező | `F` | `FMailbox`, `FState` |

### `ObservableProperty` (MVVM kivétel)

A `CommunityToolkit.Mvvm` `ObservableProperty` használatánál az aláhúzás prefix `_` kötelező a generátor miatt:

```csharp
[ObservableProperty]
private string _configName = string.Empty;  // generált property: ConfigName
```

## Kétnyelvű XML dokumentáció

Minden publikus tagra kötelező a magyar + angol XML dokumentáció:

```csharp
/// <summary>
/// hu: Magyar leírás
/// <br />
/// en: English description
/// </summary>
public void DoSomething(string AInput) { }
```

## Kód formázás

Üres sor vezérlési szerkezetek előtt és után (`if`, `while`, `for`, `switch`, `try`):

```csharp
// ✅ helyes
public void Process()
{
    var x = 1;
    var y = 2;

    if (x > 0)
        DoA();

    var z = 3;
}
```

Kivétel: egymás utáni vezérlési szerkezetek között elég egy üres sor.

## Commit üzenetek

Formátum: `<típus>: Rövid leírás`

Típusok: `fix`, `feat`, `refactor`, `docs`, `chore`, `test`, `perf`.

Példák:
- `feat: TMailbox thread-safe Post implementáció`
- `test: TActorRef egyenlőség tesztek`
- `fix: TActorSystem.Shutdown race condition`

Kétnyelvű commit üzenet ajánlott (magyar először, utána angol külön sorban) ha a változás felhasználó-oldali vagy architekturális hatású.

## Hardver / CFPU visszajelzés

Ha a Symphact-en dolgozva felfedezel egy követelményt, amit a CFPU hardvernek biztosítania kellene (mailbox mélység, interrupt struktúra, capability szélesség, stb.), nyiss issue-t az [`osreq-for-cfpu`](.github/ISSUE_TEMPLATE/osreq-for-cfpu.md) sablonnal. Ezek az issue-k közvetlenül a [`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU/blob/main/README-hu.md) hardveres tervezési viták bemenetét képezik.

## Pull request ellenőrzőlista

- [ ] Minden meglévő teszt zöld
- [ ] Új viselkedéshez új teszt
- [ ] `TreatWarningsAsErrors=true` — nincs warning
- [ ] Publikus tagokon kétnyelvű XML doc
- [ ] Elnevezési konvenciók (T, I, A, F) követve
- [ ] Commit üzenetek világosak és típusozottak
