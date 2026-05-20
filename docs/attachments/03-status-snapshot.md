# Attachment 3 — Current Status Snapshot

> NLnet NGI Zero Commons Fund — Symphact application, 13th open call (deadline 2026-06-01).
> Snapshot date: **2026-05-19**. Repo: <https://github.com/FenySoft/Symphact>. Branch: `main`.

## 1. Milestone status at submission

| Milestone | Status | Tests | Delivered |
|---|---|---|---|
| **M0.1** Actor core primitives | ✅ Shipped | 30 | `IMailbox`/`TMailbox`, `TActorRef`, `TActor<TState>`, `TActorSystem` |
| **M0.2** ActorContext + inter-actor messaging | ✅ Shipped | +16 | `IActorContext`/`TActorContext`, `DrainAsync` with `MaxRounds` |
| **M0.3** Supervision (let-it-crash) | ✅ Shipped | +30 | `ISupervisorStrategy`, `TOneForOneStrategy`, lifecycle hooks, hierarchy |
| **M0.4** Scheduler + per-actor parallelism | ✅ Shipped | +86 | `IScheduler`, `TInlineScheduler`, `TDedicatedThreadScheduler`, `IMailboxSignal`/`TDotNetMailboxSignal`, `QuiesceAsync` |
| **M0.5** Persistence — BCL-only slices | ✅ Shipped | +44 | `IJournal`+`TInMemoryJournal`, `ISnapshotStore`+`TInMemorySnapshotStore` |
| **M0.5** Persistence — content-addressed | 🚧 In progress | — | `TCasJournal`+`TCasSnapshotStore` (SHA-256 content-addressed) — *grant deliverable M1* |
| **M0.6** Remoting + capability registry | ⏳ Planned | — | `ITransport`+`TTcpTransport`, `TCapabilityRegistry` — *grant deliverable M2* |
| **M0.7** CFPU integration demo | ⏳ Planned | — | End-to-end demo over `FenySoft.CilCpu.Sim` — *grant deliverable M4* |

**M0.3 supervision, M0.4 scheduler and the M0.5 BCL-only slices shipped between v0.1 (2026-04-16) and this submission, ~65 hours of self-funded TDD work. These are explicitly out of grant scope** — the grant funds the content-addressed production-grade journal, supervision lifecycle integration, remoting, capability registry, kernel + device actors, CFPU integration, formal-verification groundwork, and outreach.

## 2. Test suite — 186 green xUnit tests

```
$ dotnet test --nologo
Passed!  - Failed:     0, Passed:   120, Skipped:     0, Total:   120 - Symphact.Core.Tests.dll
Passed!  - Failed:     0, Passed:    22, Skipped:     0, Total:    22 - Symphact.Platform.DotNet.Tests.dll
Passed!  - Failed:     0, Passed:    44, Skipped:     0, Total:    44 - Symphact.Persistence.Tests.dll
```

| Test project | Count | Coverage |
|---|---|---|
| `Symphact.Core.Tests` | **120** | Actor core, scheduler, supervision, concurrency stress, mailbox signal |
| `Symphact.Platform.DotNet.Tests` | **22** | `TMailbox` (ConcurrentQueue MPMC), `TDotNetMailboxSignal` (AutoResetEvent WFI) |
| `Symphact.Persistence.Tests` | **44** | `TInMemoryJournal`, `TInMemorySnapshotStore` (append-only, replay, snapshot semantics) |
| **Total** | **186** | |

CI runs the full suite on **Ubuntu / Windows / macOS** against .NET 10 SDK on every push (`.github/workflows/ci.yml`).

## 3. Code metrics

| Layer | Files | Lines (.cs) |
|---|---|---|
| `src/Symphact.Core/` | 18 | runtime primitives, interfaces, supervision, scheduler |
| `src/Symphact.Persistence/` | 6 | journal + snapshot store interfaces + in-memory impls |
| `src/Symphact.Platform.DotNet/` | 3 | platform-specific .NET mailbox + signal impl |
| `src/Symphact.Platform.Cfpu/` | (stub) | awaiting CFPU F4 multi-core |
| **`src/` total** | **~27 files** | **~2 975 LOC** |
| `tests/` total | **~20 files** | **~3 890 LOC** |
| **Test-to-runtime ratio** | | **~1.31×** (strict TDD discipline) |

Build configuration (`Directory.Build.props`): `.NET 10`, `TreatWarningsAsErrors=true`, `GenerateDocumentationFile=true` — every warning is build-breaking, every public member requires XML docs.

## 4. Repository activity

- **Project start:** 2026-04-16 (initial repo scaffolding)
- **Snapshot date:** 2026-05-19
- **Calendar weeks:** ~5 weeks
- **Total commits:** 38
- **Focused TDD hours:** ~65 hours
- **Lines per hour:** ~106 (runtime + tests combined)

Recent commits (most recent first):

```
ac14a17 docs: NLnet pályázat v1.3 — post-Dennard paradigma-átírás + ördögügyvédi tényellenőrzés
396cbd2 feat: M0.5 második szelet — ISnapshotStore + TInMemorySnapshotStore (BCL-only)
1750e39 feat: M0.5 első szelet — IJournal + TInMemoryJournal (BCL-only)
dc236c4 docs: „1 core = 1 actor" helytelen állítás javítása + roadmap szétválasztás
db29cd1 docs: NLnet draft — PQC pontosítás LMS (NIST SP 800-208) hash-based signature-re
fc44d9b docs: NLnet pályázati draft v1.1 — beadás-kész 13. nyílt körre
e719fbe feat: M0.4 Scheduler + per-aktor parallelizmus
8e61fc1 docs: Armstrong–Hewitt hivatkozások hitelességi javítása a vision dokumentumokban
91eb5d0 docs: HMAC → CST modell átvezetés a Symphact dokumentációban
5f5443c feat: M0.3 Supervision + HAL platform architektúra
907bd46 feat: TActorRef(int SlotIndex) + CST capability modell — kaszkád átvezetés
```

## 5. Source-file inventory (runtime)

**`src/Symphact.Core/`** (18 files):
```
ECoreStatus.cs                 IPlatform.cs              TActorRef.cs
ESupervisorDirective.cs        IScheduler.cs             TActorSystem.cs
IActorContext.cs               ISchedulerHost.cs         TDedicatedThreadScheduler.cs
IMailbox.cs                    ISupervisorStrategy.cs    TInlineScheduler.cs
IMailboxSignal.cs              TActor.cs                 TOneForOneStrategy.cs
IMailboxSignalProvider.cs      TActorContext.cs          TTerminated.cs
```

**`src/Symphact.Persistence/`** (6 files):
```
IJournal.cs        ISnapshotStore.cs        TInMemoryJournal.cs
TJournalEntry.cs   TSnapshotEntry.cs        TInMemorySnapshotStore.cs
```

**`src/Symphact.Platform.DotNet/`** (3 files):
```
TDotNetMailboxSignal.cs   TDotNetPlatform.cs   TMailbox.cs
```

## 6. Build verification

```
$ dotnet build Symphact.sln -c Debug
  Symphact.Core            -> bin/Debug/net10.0/Symphact.Core.dll
  Symphact.Persistence     -> bin/Debug/net10.0/Symphact.Persistence.dll
  Symphact.Platform.DotNet -> bin/Debug/net10.0/Symphact.Platform.DotNet.dll
  Symphact.Core.Tests            -> bin/Debug/net10.0/Symphact.Core.Tests.dll
  Symphact.Persistence.Tests     -> bin/Debug/net10.0/Symphact.Persistence.Tests.dll
  Symphact.Platform.DotNet.Tests -> bin/Debug/net10.0/Symphact.Platform.DotNet.Tests.dll
  Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Zero warnings on a `TreatWarningsAsErrors=true` build with `GenerateDocumentationFile=true` — every public member is documented bilingually (`hu:` / `en:`) per repo convention.

## 7. Documentation state

| Document | Version | Lines (en/hu) | Status |
|---|---|---|---|
| `docs/vision-en.md` / `vision-hu.md` | 1.4 | ~750 / ~750 | ✅ Complete |
| `docs/roadmap-en.md` / `roadmap-hu.md` | 1.0 | ~750 / ~750 | ✅ Complete |
| `docs/trust-model-en.md` / `trust-model-hu.md` | 1.1 | ~250 / ~250 | ✅ Complete |
| `docs/boot-sequence-en.md` / `boot-sequence-hu.md` | — | — | ✅ Complete |
| `docs/ddr5-memory-model-en.md` / `ddr5-memory-model-hu.md` | — | — | ✅ Complete |
| `docs/actor-ref-scaling-en.md` / `actor-ref-scaling-hu.md` | — | — | ✅ Complete |
| `docs/osreq-to-cfpu/osreq-001..006` | — | bilingual | ✅ Active (007 superseded by CST model) |
| `docs/nlnet-application-draft-en.md` / `-hu.md` | 1.3 | 302 / 350 | ✅ Submission-ready |

Every document is bilingual (English + Hungarian) per repo convention. Cross-reference integrity is maintained across the two language sets.
