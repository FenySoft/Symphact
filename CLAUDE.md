# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**Symphact** — capability-based actor runtime for .NET, co-designed with the Cognitive Fabric Processing Unit (CFPU). Today a reference runtime on any .NET host; tomorrow runs natively on CFPU silicon where each core is physically an actor with private SRAM and hardware mailbox FIFOs. Apache-2.0.

Version: **0.1 (pre-alpha)** — the actor core (mailbox / ref / actor / system) is the first milestone; no supervision, multi-core scheduling, persistence, or distribution yet.

Sister repo: [`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU) (CFPU hardware, CERN-OHL-S). OS → HW feedback flows via `docs/osreq-to-cfpu/` and the `osreq-for-cfpu` issue template.

## Build / test commands

```bash
# full build
dotnet build Symphact.sln -c Debug

# run all tests
dotnet test

# run a single test class
dotnet test --filter "FullyQualifiedName~TMailboxTests"

# run a single test method
dotnet test --filter "FullyQualifiedName~TMailboxTests.Post_ConcurrentWritesAreThreadSafe"

# incremental build (once restored)
dotnet build Symphact.sln -c Debug --no-restore
```

CI runs on ubuntu / windows / macos against **.NET 10 SDK** (`.github/workflows/ci.yml`).
`Directory.Build.props` sets `TreatWarningsAsErrors=true` + `GenerateDocumentationFile=true` for the whole solution — all warnings are build-breaking, and all public members need XML docs.

## Architecture

Three primitives form the core; understanding their contracts is enough to navigate the codebase:

1. **`IMailbox` / `TMailbox`** (`src/Symphact.Core/IMailbox.cs`, `TMailbox.cs`)
   FIFO mailbox with `Post` / `TryReceive`. `TMailbox` is the in-memory reference impl on `ConcurrentQueue<object>` (lock-free MPMC). Designed so a future `TMmioMailbox` against CFPU hardware FIFO can drop in without API changes — keep the interface narrow and thread-safe.

2. **`TActorRef`** (`src/Symphact.Core/TActorRef.cs`)
   `readonly record struct TActorRef(long ActorId)` — value-type capability token. `default` is invalid (`IsValid=false`, `TActorRef.Invalid`). Equality/hash by `ActorId`. The single-integer form is a placeholder — the future shape (`core-id + mailbox offset + HMAC tag + permissions`) must preserve the same public surface.

3. **`TActor<TState>` + `TActorSystem`** (`src/Symphact.Core/TActor.cs`, `TActorSystem.cs`)
   - `TActor<TState>` abstract: `Init()` returns initial state; `Handle(state, msg)` returns new state. Handlers must be side-effect-free — the only legitimate "side-effect" (sending messages) will arrive via a future context parameter.
   - `TActorSystem` is the runtime: `Spawn<TActorType, TState>()` → `TActorRef`; `Send(ref, msg)` enqueues; `DrainAsync()` processes every mailbox in rounds until empty (multi-round because handlers may eventually post to each other); `GetState<T>(ref)` is diagnostics-only (in a real system state is private).
   - This is a **single-host, single-threaded reference drain**. Multi-core per-actor scheduling, supervision, persistence, remoting are deferred. Don't add them opportunistically — each is its own milestone.

### Design invariants (do not violate without an architecture discussion)

- **No shared memory between actors.** State lives inside the actor; messages are immutable objects crossing mailboxes.
- **Let it crash.** Defensive error handling is an anti-pattern here; the model is supervision (not yet implemented, but its future presence informs API shape).
- **Location transparency.** `TActorRef` must not leak whether the target is local, remote, or on another chip.
- **Determinism.** Same input sequence + same initial state → same final state. Handlers must be pure w.r.t. the state they return.
- **Capability = reference.** Holding a `TActorRef` is the authorization to send; there is no ambient actor lookup.

### Project layout

```
src/Symphact.Core/              runtime (only project today)
tests/Symphact.Core.Tests/      xUnit, mirrors src/ structure
samples/                        (empty — CounterActor demo is the first target)
docs/osreq-to-cfpu/             OS→HW requirements feeding CLI-CPU
.github/workflows/ci.yml        multi-OS build+test
Directory.Build.props           net10.0, warnings-as-errors, docs on
```

## TDD is enforced (not optional here)

`CONTRIBUTING.md` mandates test-first. Concretely this means: **do not add/modify runtime code in `src/Symphact.Core/` without a corresponding failing test first in `tests/Symphact.Core.Tests/`.** Exceptions are limited to `.csproj`, docs, workflow files. An assertion-free test counts as no test.

If you touch existing untested code, add the tests before modifying.

## Conventions (project-specific reminders)

Most global conventions (naming `T/I/A/F`, bilingual XML docs, blank lines around control structures, commit prefix types) are listed in the user's global `~/.claude/CLAUDE.md` and this repo's `CONTRIBUTING.md`. Project-specific notes:

- **`TreatWarningsAsErrors=true`** is repo-wide — a missing XML doc on a public member will fail the build (`CS1591` is explicitly un-suppressed for public API; currently only suppressed via `NoWarn` where you see it, so: document public members).
- **Actor-specific rule:** there is currently no `ActorContext` — handlers receive `(state, message)` only. Do not sneak in cross-actor calls via captured fields; wait for the context parameter to be introduced as a deliberate API step.
- **`TActorSystem.GetState` is test-only.** Production code must not read another actor's state directly — use a message round-trip.
- **Every new runtime type belongs behind an interface if it has a hardware-backed future variant** (mailbox is the template: `IMailbox` + `TMailbox`). This keeps the CFPU drop-in path clean.
