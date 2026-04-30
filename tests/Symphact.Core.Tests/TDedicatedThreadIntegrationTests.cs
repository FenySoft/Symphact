using Symphact.Core;
using Symphact.Core.Tests.Helpers;

namespace Symphact.Core.Tests;

/// <summary>
/// hu: A TActorSystem és TDedicatedThreadScheduler end-to-end integrációs tesztjei
/// (M0.4 — C.7). Valós aktor-handler-ek dedikált thread-eken futnak; a QuiesceAsync
/// barrier szinkronizálja a tesztet a feldolgozással. Cross-thread supervision (Restart,
/// Stop, Escalate) szinkron a crash-elt aktor szálán fut — nincs TFailureEnvelope.
/// <br />
/// en: End-to-end integration tests for TActorSystem and TDedicatedThreadScheduler
/// (M0.4 — C.7). Real actor handlers execute on dedicated threads; the QuiesceAsync
/// barrier synchronises the test with processing. Cross-thread supervision (Restart,
/// Stop, Escalate) runs synchronously on the crashed actor's thread — no TFailureEnvelope.
/// </summary>
public sealed class TDedicatedThreadIntegrationTests
{
    private sealed class TCounterActor : TActor<int>
    {
        public override int Init() => 0;

        public override int Handle(int AState, object AMessage, IActorContext AContext) => AMessage switch
        {
            "increment" => AState + 1,
            _ => AState
        };
    }

    private sealed class TFailingActor : TActor<int>
    {
        public override int Init() => 0;

        public override ISupervisorStrategy SupervisorStrategy =>
            new TOneForOneStrategy(_ => ESupervisorDirective.Restart);

        public override int Handle(int AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "fail")
                throw new InvalidOperationException("Intentional failure");

            return AState + 1;
        }
    }

    private sealed class TParentActor : TActor<TActorRef>
    {
        public override TActorRef Init() => TActorRef.Invalid;

        public override ISupervisorStrategy SupervisorStrategy =>
            new TOneForOneStrategy(_ => ESupervisorDirective.Restart);

        public override TActorRef Handle(TActorRef AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "spawn-child")
                return AContext.Spawn<TFailingActor, int>();

            if (AMessage is "get-child-ref" && AState.IsValid)
            {
                AContext.Send(AState, "increment");
                return AState;
            }

            return AState;
        }
    }

    [Fact]
    public async Task Send_SingleActor_ProcessesAllMessagesOnDedicatedThread()
    {
        using var system = new TActorSystem(
            new TDotNetPlatform(),
            new TDedicatedThreadScheduler(new TDotNetPlatform()));
        var actorRef = system.Spawn<TCounterActor, int>();

        for (var i = 0; i < 100; i++)
            system.Send(actorRef, "increment");

        await system.QuiesceAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(100, system.GetState<int>(actorRef));
    }

    [Fact]
    public async Task Send_MultipleActors_EachProcessedOnOwnThread()
    {
        const int actorCount = 10;
        const int messagesPerActor = 50;

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

        await system.QuiesceAsync(TimeSpan.FromSeconds(5));

        for (var i = 0; i < actorCount; i++)
            Assert.Equal(messagesPerActor, system.GetState<int>(refs[i]));
    }

    [Fact]
    public async Task FailingActor_OnDedicatedThread_RestartsAfterCrash()
    {
        using var system = new TActorSystem(
            new TDotNetPlatform(),
            new TDedicatedThreadScheduler(new TDotNetPlatform()));
        var parentRef = system.Spawn<TParentActor, TActorRef>();

        system.Send(parentRef, "spawn-child");
        await system.QuiesceAsync(TimeSpan.FromSeconds(5));

        var childRef = system.GetState<TActorRef>(parentRef);
        Assert.True(childRef.IsValid);

        system.Send(childRef, "increment");
        system.Send(childRef, "increment");
        system.Send(childRef, "fail");
        system.Send(childRef, "increment");

        await system.QuiesceAsync(TimeSpan.FromSeconds(5));

        // Restart re-runs Init() → state is back to 0 + the post-restart increment
        Assert.Equal(1, system.GetState<int>(childRef));
        Assert.False(system.IsStopped(childRef));
    }

    [Fact]
    public async Task Send_FromMultipleThreads_ToSingleActor_NoMessageLost()
    {
        const int threadCount = 4;
        const int messagesPerThread = 200;

        using var system = new TActorSystem(
            new TDotNetPlatform(),
            new TDedicatedThreadScheduler(new TDotNetPlatform()));
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

        await system.QuiesceAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(threadCount * messagesPerThread, system.GetState<int>(actorRef));
    }

    [Fact]
    public async Task QuiesceAsync_Empty_ReturnsImmediately()
    {
        using var system = new TActorSystem(
            new TDotNetPlatform(),
            new TDedicatedThreadScheduler(new TDotNetPlatform()));

        await system.QuiesceAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task QuiesceAsync_AfterDispose_ThrowsObjectDisposed()
    {
        var system = new TActorSystem(
            new TDotNetPlatform(),
            new TDedicatedThreadScheduler(new TDotNetPlatform()));
        system.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => system.QuiesceAsync(TimeSpan.FromSeconds(1)));
    }
}
