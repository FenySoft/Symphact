# Symphact.Core

**Capability-based actor runtime for .NET** — co-designed with the Cognitive Fabric Processing Unit (CFPU).

> Every stateful entity is an actor. Actors communicate exclusively through immutable messages over
> mailboxes. Isolation is enforced by the runtime on .NET hosts, and as a hardware property on CFPU.

This package contains the runtime core and the hardware abstraction interfaces. It has **no external
dependencies** — BCL only.

## Status: pre-alpha

The API is not stable yet. Actor core, supervision (let-it-crash), and the scheduler API are complete
and covered by tests; remoting, capability registry, and CFPU integration are not.

## You also need a platform package

`Symphact.Core` alone cannot start a system: `TActorSystem` requires an `IPlatform` implementation and
the core package ships none. Install a platform package alongside it:

```
dotnet add package FenySoft.Symphact.Core
dotnet add package FenySoft.Symphact.Platform.DotNet
```

`FenySoft.Symphact.Platform.DotNet` provides `TDotNetPlatform` — `ConcurrentQueue`-based mailboxes and
`AutoResetEvent` signalling, with no hardware limits.

## Example

```csharp
using Symphact.Core;
using Symphact.Platform.DotNet;
using Demo;

using var system = new TActorSystem(new TDotNetPlatform());

var counter = system.Spawn<TCounterActor, int>();
system.Send(counter, new MsgIncrement());
system.Drain();

Console.WriteLine(system.GetState<int>(counter));   // 1

namespace Demo
{
    internal sealed record MsgIncrement;

    internal sealed class TCounterActor : TActor<int>
    {
        public override int Init() => 0;

        public override int Handle(int AState, object AMessage, IActorContext AContext)
            => AMessage is MsgIncrement ? AState + 1 : AState;
    }
}
```

A full runnable demo (Increment / Decrement / Query with a reply actor) lives in
[`samples/CounterActor`](https://github.com/FenySoft/Symphact/tree/main/samples/CounterActor).

## What is in this package

| Primitive | Type |
|-----------|------|
| Mailbox | `IMailbox` — FIFO `Post` / `TryReceive`; the concrete implementation comes from the platform package |
| Actor reference | `TActorRef` — `readonly record struct`, opaque capability slot index; `default` is invalid |
| Actor | `TActor<TState>` — `Init()` returns the initial state, `Handle(state, message, context)` returns the next |
| System | `TActorSystem` — `Spawn`, `Send`, `Drain`, `DrainAsync`, `QuiesceAsync` |
| Supervision | `ISupervisorStrategy`, `TOneForOneStrategy`, `TAllForOneStrategy`, lifecycle hooks, `TTerminated` |
| Scheduling | `IScheduler`, `TInlineScheduler` (single-threaded reference), `TDedicatedThreadScheduler` (one OS thread per actor, CFPU dedicated-core simulation) |
| Platform HAL | `IPlatform`, `IMailboxSignal`, `IMailboxSignalProvider` — implemented by platform packages |

Persistence (`IJournal`, `ISnapshotStore`) ships separately in `FenySoft.Symphact.Persistence`.

## Design principles

1. **Everything is an actor.** Drivers, supervisors, services, business logic.
2. **No shared memory.** Only immutable messages through mailboxes.
3. **Let it crash.** Failures are handled by supervisors, not by defensive error handling.
4. **Location transparency.** `TActorRef` does not reveal whether the target is local, remote, or on another chip.
5. **Capability-based security.** Holding a reference *is* the authorization to send.
6. **Per-actor determinism.** Each actor sees its messages in FIFO order; `TInlineScheduler` additionally preserves strict global ordering.

## Links

- Source, roadmap and documentation: <https://github.com/FenySoft/Symphact>
- Sister hardware project (CFPU reference implementation): <https://github.com/FenySoft/CLI-CPU>
- License: Apache-2.0
