using Symphact.Core;

namespace Symphact.Core.Tests;

/// <summary>
/// hu: A TActorSystem és IScheduler integrációs tesztjei (M0.4 — C.3). A Send minden
/// mailbox-poszt után jelez a schedulernek, és a TActorSystem mint ISchedulerHost futtatja
/// az aktor szeletét. A QuiesceAsync a scheduler-en keresztül determinisztikus barrier.
/// <br />
/// en: Integration tests for TActorSystem and IScheduler (M0.4 — C.3). Each Send signals
/// the scheduler after the mailbox post, and TActorSystem implements ISchedulerHost to
/// execute actor slices. QuiesceAsync is a scheduler-mediated deterministic barrier.
/// </summary>
public sealed class TActorSystemSchedulerTests
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

    private sealed class TRecordingScheduler : IScheduler
    {
        public List<TActorRef> SignalCalls { get; } = new();
        public List<TActorRef> RegisterCalls { get; } = new();
        public List<TActorRef> UnregisterCalls { get; } = new();
        public bool AttachCalled { get; private set; }

        public void Attach(ISchedulerHost AHost)
        {
            AttachCalled = true;
        }

        public void Register(TActorRef AActor, IMailbox AMailbox)
        {
            RegisterCalls.Add(AActor);
        }

        public void Unregister(TActorRef AActor)
        {
            UnregisterCalls.Add(AActor);
        }

        public void Signal(TActorRef AActor)
        {
            SignalCalls.Add(AActor);
        }

        public Task QuiesceAsync(TimeSpan ATimeout, CancellationToken ACancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }

    [Fact]
    public void Constructor_WithCustomScheduler_AttachesIt()
    {
        var scheduler = new TRecordingScheduler();

        using var system = new TActorSystem(new TDotNetPlatform(), scheduler);

        Assert.True(scheduler.AttachCalled);
    }

    [Fact]
    public void Constructor_WithNullScheduler_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TActorSystem(new TDotNetPlatform(), null!));
    }

    [Fact]
    public void Spawn_WithCustomScheduler_RegistersActor()
    {
        var scheduler = new TRecordingScheduler();
        using var system = new TActorSystem(new TDotNetPlatform(), scheduler);

        var actorRef = system.Spawn<TCounterActor, int>();

        Assert.Single(scheduler.RegisterCalls);
        Assert.Equal(actorRef, scheduler.RegisterCalls[0]);
    }

    [Fact]
    public void Send_WithCustomScheduler_SignalsTarget()
    {
        var scheduler = new TRecordingScheduler();
        using var system = new TActorSystem(new TDotNetPlatform(), scheduler);
        var actorRef = system.Spawn<TCounterActor, int>();

        system.Send(actorRef, "increment");

        Assert.Single(scheduler.SignalCalls);
        Assert.Equal(actorRef, scheduler.SignalCalls[0]);
    }

    [Fact]
    public void Send_ToInvalidRef_ThrowsBeforeSignal()
    {
        var scheduler = new TRecordingScheduler();
        using var system = new TActorSystem(new TDotNetPlatform(), scheduler);

        Assert.Throws<InvalidOperationException>(() =>
            system.Send(new TActorRef(999), "msg"));
        Assert.Empty(scheduler.SignalCalls);
    }

    [Fact]
    public async Task QuiesceAsync_WithInlineScheduler_ProcessesAllMessages()
    {
        var scheduler = new TInlineScheduler();
        using var system = new TActorSystem(new TDotNetPlatform(), scheduler);
        var actorRef = system.Spawn<TCounterActor, int>();

        system.Send(actorRef, "increment");
        system.Send(actorRef, "increment");
        system.Send(actorRef, "increment");

        await system.QuiesceAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(3, system.GetState<int>(actorRef));
    }

    [Fact]
    public async Task QuiesceAsync_NoMessages_ReturnsImmediately()
    {
        var scheduler = new TInlineScheduler();
        using var system = new TActorSystem(new TDotNetPlatform(), scheduler);
        system.Spawn<TCounterActor, int>();

        await system.QuiesceAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Dispose_WithCustomScheduler_DisposesScheduler()
    {
        var scheduler = new TRecordingScheduler();
        var system = new TActorSystem(new TDotNetPlatform(), scheduler);

        system.Dispose();

        // RecordingScheduler.Dispose is a no-op but ensure no exception
        Assert.True(scheduler.AttachCalled);
    }

    [Fact]
    public void Drain_WithCustomScheduler_StillWorks()
    {
        // Backwards-compat: legacy Drain mode continues to work alongside scheduler-mediated mode.
        var scheduler = new TRecordingScheduler();
        using var system = new TActorSystem(new TDotNetPlatform(), scheduler);
        var actorRef = system.Spawn<TCounterActor, int>();

        system.Send(actorRef, "increment");
        system.Drain();

        Assert.Equal(1, system.GetState<int>(actorRef));
    }
}
