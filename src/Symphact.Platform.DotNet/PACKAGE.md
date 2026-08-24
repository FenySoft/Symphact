# Symphact.Platform.DotNet

**.NET platform implementation for the [Symphact](https://github.com/FenySoft/Symphact) actor runtime.**

Symphact is a capability-based actor runtime co-designed with the Cognitive Fabric Processing Unit
(CFPU). `Symphact.Core` defines the runtime and its hardware abstraction (`IPlatform`); this package
provides the implementation that runs on any .NET host — Windows, Linux, macOS.

## You need both packages

`Symphact.Core` alone cannot start a system: `TActorSystem` requires an `IPlatform` and the core
package ships none.

```
dotnet add package FenySoft.Symphact.Core
dotnet add package FenySoft.Symphact.Platform.DotNet
```

## Status: pre-alpha

The API is not stable yet.

## What is in this package

| Type | Role |
|------|------|
| `TDotNetPlatform` | `IPlatform` + `IMailboxSignalProvider` — creates mailboxes and signals for the actor system |
| `TMailbox` | FIFO mailbox on `ConcurrentQueue<object>` (lock-free MPMC), no capacity limit |
| `TDotNetMailboxSignal` | `IMailboxSignal` on `AutoResetEvent`; latching, and cancellable via `CancellationToken` |

This is the *unconstrained* platform: no hardware mailbox depth, no SRAM budget, no CST capability
check. Those constraints appear on the CFPU platform, where the same `IPlatform` surface is backed by
hardware FIFOs.

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

For multi-threaded execution — one OS thread per actor, simulating the CFPU dedicated-core model —
pass a scheduler:

```csharp
var platform = new TDotNetPlatform();

using var system = new TActorSystem(platform, new TDedicatedThreadScheduler(platform));
```

A full runnable demo lives in
[`samples/CounterActor`](https://github.com/FenySoft/Symphact/tree/main/samples/CounterActor).

## Links

- Source, roadmap and documentation: <https://github.com/FenySoft/Symphact>
- Sister hardware project (CFPU reference implementation): <https://github.com/FenySoft/CLI-CPU>
- License: Apache-2.0
