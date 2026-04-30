using Symphact.Core;
using Symphact.Core.Tests.Helpers;

namespace Symphact.Core.Tests;

/// <summary>
/// hu: A scheduler-implementációk stressz/race tesztjei (M0.4 — C.8). Sok aktor, sok
/// üzenet, sok thread egy időben — a race-mentesség és a deadlock-szabadság CI-validációja.
/// Egyetlen flaky futás itt elég, hogy a teljes M0.4 garanciát kétségbe vonjuk.
/// <br />
/// en: Stress / race tests for scheduler implementations (M0.4 — C.8). Many actors, many
/// messages, many concurrent threads — CI validation of race-freedom and deadlock-freedom.
/// A single flaky run here is enough to undermine all M0.4 guarantees.
/// </summary>
public sealed class TSchedulerStressTests
{
    private sealed record TPingPongState(TActorRef Partner, int Count);

    private sealed class TPingPongActor : TActor<TPingPongState>
    {
        public override TPingPongState Init() => new(TActorRef.Invalid, 0);

        public override TPingPongState Handle(TPingPongState AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is TActorRef partner)
                return AState with { Partner = partner };

            if (AMessage is int hopsLeft && hopsLeft > 0 && AState.Partner.IsValid)
            {
                AContext.Send(AState.Partner, hopsLeft - 1);
                return AState with { Count = AState.Count + 1 };
            }

            return AState;
        }
    }

    private sealed class TCounterActor : TActor<int>
    {
        public override int Init() => 0;

        public override int Handle(int AState, object AMessage, IActorContext AContext) => AMessage switch
        {
            "increment" => AState + 1,
            _ => AState
        };
    }

    [Fact]
    public async Task DedicatedThread_100ActorsHighThroughput_AllProcess()
    {
        const int actorCount = 100;
        const int messagesPerActor = 500;

        using var system = new TActorSystem(
            new TDotNetPlatform(),
            new TDedicatedThreadScheduler(new TDotNetPlatform()));
        var refs = new TActorRef[actorCount];

        for (var i = 0; i < actorCount; i++)
            refs[i] = system.Spawn<TCounterActor, int>();

        for (var i = 0; i < actorCount; i++)
        {
            for (var j = 0; j < messagesPerActor; j++)
                system.Send(refs[i], "increment");
        }

        await system.QuiesceAsync(TimeSpan.FromSeconds(30));

        for (var i = 0; i < actorCount; i++)
            Assert.Equal(messagesPerActor, system.GetState<int>(refs[i]));
    }

    [Fact]
    public async Task DedicatedThread_PingPongPair_HighHops_NoDeadlock()
    {
        const int hops = 500;

        using var system = new TActorSystem(
            new TDotNetPlatform(),
            new TDedicatedThreadScheduler(new TDotNetPlatform()));
        var aRef = system.Spawn<TPingPongActor, TPingPongState>();
        var bRef = system.Spawn<TPingPongActor, TPingPongState>();

        system.Send(aRef, bRef);
        system.Send(bRef, aRef);
        system.Send(aRef, hops);

        await system.QuiesceAsync(TimeSpan.FromSeconds(30));

        var aState = system.GetState<TPingPongState>(aRef);
        var bState = system.GetState<TPingPongState>(bRef);

        // Az egyik 250 hop-ot fogad, a másik 250-et — összegben 500 (vagy 499 + 1, hops parítástól függően)
        Assert.Equal(hops, aState.Count + bState.Count);
    }

    [Fact]
    public async Task DedicatedThread_ManyPingPongPairs_NoDeadlock()
    {
        const int pairCount = 10;
        const int hopsPerPair = 100;

        using var system = new TActorSystem(
            new TDotNetPlatform(),
            new TDedicatedThreadScheduler(new TDotNetPlatform()));
        var pairs = new (TActorRef A, TActorRef B)[pairCount];

        for (var i = 0; i < pairCount; i++)
        {
            var a = system.Spawn<TPingPongActor, TPingPongState>();
            var b = system.Spawn<TPingPongActor, TPingPongState>();
            pairs[i] = (a, b);

            system.Send(a, b);
            system.Send(b, a);
        }

        for (var i = 0; i < pairCount; i++)
            system.Send(pairs[i].A, hopsPerPair);

        await system.QuiesceAsync(TimeSpan.FromSeconds(30));

        var totalHops = 0;

        for (var i = 0; i < pairCount; i++)
        {
            totalHops += system.GetState<TPingPongState>(pairs[i].A).Count;
            totalHops += system.GetState<TPingPongState>(pairs[i].B).Count;
        }

        Assert.Equal(pairCount * hopsPerPair, totalHops);
    }

    [Fact]
    public async Task Inline_HighSendVolumeFromManyThreads_NoMessageLost()
    {
        const int threadCount = 8;
        const int messagesPerThread = 500;

        using var system = new TActorSystem(new TDotNetPlatform());
        var actorRef = system.Spawn<TCounterActor, int>();
        var barrier = new Barrier(threadCount);
        var threads = new Thread[threadCount];

        for (var i = 0; i < threadCount; i++)
        {
            threads[i] = new Thread(() =>
            {
                barrier.SignalAndWait();

                for (var j = 0; j < messagesPerThread; j++)
                    system.Send(actorRef, "increment");
            });

            threads[i].Start();
        }

        foreach (var t in threads)
            t.Join();

        await system.QuiesceAsync(TimeSpan.FromSeconds(15));

        Assert.Equal(threadCount * messagesPerThread, system.GetState<int>(actorRef));
    }

    [Fact]
    public async Task DedicatedThread_DisposeWith50Actors_AllThreadsStopWithin5s()
    {
        var system = new TActorSystem(
            new TDotNetPlatform(),
            new TDedicatedThreadScheduler(new TDotNetPlatform()));

        for (var i = 0; i < 50; i++)
            system.Spawn<TCounterActor, int>();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        system.Dispose();
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"Dispose took {sw.Elapsed} for 50 actors; expected < 5s.");

        await Task.CompletedTask;
    }
}
