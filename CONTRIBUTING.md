# Contributing to Symphact

> Magyar verzió: [CONTRIBUTING-hu.md](CONTRIBUTING-hu.md)

Thank you for your interest in contributing to Symphact!

## Prerequisites

- **.NET 10.0 SDK** or later
- **Git**
- An editor: Visual Studio, VS Code, JetBrains Rider, or any editor supporting C# 13

## Build and test

```bash
# Full build
dotnet build Symphact.sln -c Debug

# Run all tests
dotnet test

# Run a specific test
dotnet test --filter "FullyQualifiedName~TMailboxTests"

# Build without restore (if previously restored)
dotnet build Symphact.sln -c Debug --no-restore
```

## Test-driven development (TDD) is required

Every new feature, bug fix, or refactor must be **test-first**:

1. **Write the test** — describe the desired behaviour in one or more xUnit tests
2. **Run it** — the test must **fail** (red) — this confirms the test is real
3. **Implement** — write the minimum code that turns the test green
4. **Refactor** — clean up, tests must remain green
5. **Commit** — only with all tests green

### Rules

- ❌ **Do not** commit without tests (exceptions: configuration files, documentation)
- ❌ **Do not** write assertion-free tests (an always-green test is not a test)
- ✅ **Must** cover: actor logic, mailbox implementations, capability operations, supervisors
- ✅ **Must** create the test project if it does not yet exist
- ✅ If you touch existing code without tests → **first** add tests, **then** modify

## Naming conventions

This project follows the same conventions as [`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU) for consistency:

| Element | Prefix | Example |
|---------|--------|---------|
| Class, record, enum | `T` | `TActor`, `TMailbox`, `TActorRef` |
| Interface | `I` | `IMailbox`, `ISupervisor` |
| Method parameter | `A` | `AMessage`, `AActorRef` |
| Private / protected field | `F` | `FMailbox`, `FState` |

### `ObservableProperty` (MVVM exception)

When using `CommunityToolkit.Mvvm` `ObservableProperty`, the underscore prefix `_` is required by the generator:

```csharp
[ObservableProperty]
private string _configName = string.Empty;  // generated property: ConfigName
```

## Bilingual XML documentation

All public members must have Hungarian + English XML documentation:

```csharp
/// <summary>
/// hu: Magyar leírás
/// <br />
/// en: English description
/// </summary>
public void DoSomething(string AInput) { }
```

## Code formatting

Blank lines before and after control structures (`if`, `while`, `for`, `switch`, `try`):

```csharp
// ✅ correct
public void Process()
{
    var x = 1;
    var y = 2;

    if (x > 0)
        DoA();

    var z = 3;
}
```

Exception: consecutive control structures need only one blank line between them.

## Commit messages

Format: `<type>: Short description`

Types: `fix`, `feat`, `refactor`, `docs`, `chore`, `test`, `perf`.

Examples:
- `feat: TMailbox thread-safe Post implementation`
- `test: TActorRef equality tests`
- `fix: race condition in TActorSystem.Shutdown`

Prefer bilingual commit messages (Hungarian first, then English on a separate line) when the change has user-facing or architectural impact.

## Hardware / CFPU feedback

If while working on Symphact you discover a requirement that the CFPU hardware should provide (mailbox depth, interrupt structure, capability width, etc.), please open an issue using the [`osreq-for-cfpu`](.github/ISSUE_TEMPLATE/osreq-for-cfpu.md) template. These issues feed directly into the [`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU) hardware design discussions.

## Pull request checklist

- [ ] All existing tests are green
- [ ] New tests added for new behaviour
- [ ] `TreatWarningsAsErrors=true` — no warnings
- [ ] Bilingual XML docs on public members
- [ ] Naming conventions followed (T, I, A, F)
- [ ] Commit messages clear and typed
