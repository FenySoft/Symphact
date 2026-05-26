# CounterActor — minimal Symphact sample

> Magyar verzió alább.

A minimal, runnable Symphact demo that exercises the basic actor API:

- `TActorSystem(IPlatform)` — create the runtime
- `Spawn<TActorType, TState>()` — start an actor
- `Send(ref, msg)` — deliver a message to its mailbox
- `IActorContext.Send(...)` — actor-to-actor reply (capability token style)
- `Drain()` — synchronously process the full message graph
- `GetState<TState>(ref)` — diagnostic access (test/sample only — in production state is private)

## Run

```bash
dotnet run --project samples/CounterActor/CounterActor.csproj
```

Expected output:

```
Symphact CounterActor sample — sending 5 Increment + 2 Decrement + 1 Query

Query reply received: counter = 3
Final state via GetState<int>: 3
```

## What it shows

The sample spawns two actors:

- `TCounterActor` — `TActor<int>` holding an integer count, handling `MsgIncrement`, `MsgDecrement`, and `MsgQuery`.
- `TQueryReply` — receives the counter's reply and prints it.

The `MsgQuery` carries a `TActorRef ReplyTo` — this is **the capability token** the counter
needs to talk back to the printer. The counter does not look up the printer by name (there is
no global namespace in Symphact); the printer's capability is granted explicitly by passing it
in the message. This is the seL4 / CHERI capability model in .NET userland.

## Why this is a useful starting point

- **End-to-end actor lifecycle in ~80 lines.** Read it once, you know the surface area.
- **No global state, no static references.** Every communication goes through `Send`.
- **Drain-based execution.** Single-threaded, deterministic order — useful for tests; the
  multi-threaded `TDedicatedThreadScheduler` (M0.4) follows the same API.

For the long-running, supervised production pattern see `docs/vision-en.md` and the supervision
tests in `tests/Symphact.Core.Tests/TSupervisionTests.cs`.

---

# CounterActor — minimális Symphact minta

Egy futtatható minimal Symphact demó, ami az alapvető aktor-API-t gyakorolja:

- `TActorSystem(IPlatform)` — runtime létrehozás
- `Spawn<TActorType, TState>()` — aktor indítása
- `Send(ref, msg)` — üzenet a mailboxba
- `IActorContext.Send(...)` — aktor-aktor közötti válasz (capability-token stílus)
- `Drain()` — szinkron teljes üzenet-gráf feldolgozás
- `GetState<TState>(ref)` — diagnosztikai elérés (csak teszt/sample-hez — éles rendszerben az állapot privát)

## Futtatás

```bash
dotnet run --project samples/CounterActor/CounterActor.csproj
```

Várható kimenet:

```
Symphact CounterActor sample — sending 5 Increment + 2 Decrement + 1 Query

Query reply received: counter = 3
Final state via GetState<int>: 3
```

## Mit mutat be

A minta két aktort spawn-ol:

- `TCounterActor` — `TActor<int>` egy egész számláló állapottal, ami `MsgIncrement`, `MsgDecrement`
  és `MsgQuery` üzeneteket kezel.
- `TQueryReply` — fogadja a számláló válaszát és kiírja.

A `MsgQuery` tartalmaz egy `TActorRef ReplyTo`-t — ez **a capability token**, amit a számláló
kap, hogy visszaszólhasson a printer-aktornak. A számláló nem névvel keresi meg a printer-t
(a Symphact-ban nincs globális névtér); a printer capability-jét **explicit** kapja az üzenetben.
Ez a seL4 / CHERI capability modell .NET userland-ben.

## Miért hasznos kiindulópont

- **Teljes aktor-életciklus ~80 sorban.** Egyszer elolvasod, már látod a felületet.
- **Nincs globális állapot, statikus referencia.** Minden kommunikáció `Send`-en megy keresztül.
- **Drain-alapú végrehajtás.** Single-threaded, determinisztikus sorrend — tesztekhez ideális;
  a multi-thread `TDedicatedThreadScheduler` (M0.4) ugyanezt az API-t használja.

Hosszú, supervised éles mintáért lásd `docs/vision-hu.md` és a supervision tesztek
(`tests/Symphact.Core.Tests/TSupervisionTests.cs`).
