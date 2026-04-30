using Symphact.Core;

namespace Symphact.Core.Tests;

/// <summary>
/// hu: A TInlineScheduler skeleton kontraktusának tesztjei (M0.4 — C.1). Az inline scheduler
/// a Drain-mode szinkron, single-threaded ütemezője — a régi Drain belülről ezt fogja hívni
/// a következő ciklusokban. Itt csak a lifecycle és no-op kontraktust validáljuk.
/// <br />
/// en: Tests for the TInlineScheduler skeleton contract (M0.4 — C.1). The inline scheduler
/// is the synchronous, single-threaded scheduler for Drain-mode — the legacy Drain will
/// internally delegate to this in upcoming cycles. Here we only validate the lifecycle
/// and no-op contract.
/// </summary>
public sealed class TInlineSchedulerTests
{
    [Fact]
    public void Constructor_DoesNotThrow()
    {
        using var scheduler = new TInlineScheduler();

        Assert.NotNull(scheduler);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var scheduler = new TInlineScheduler();

        scheduler.Dispose();
        scheduler.Dispose();
    }

    [Fact]
    public void Signal_BeforeAttach_DoesNotThrow()
    {
        using var scheduler = new TInlineScheduler();

        scheduler.Signal(new TActorRef(1));
    }

    [Fact]
    public void Register_BeforeAttach_DoesNotThrow()
    {
        using var scheduler = new TInlineScheduler();
        var mailbox = new FakeMailbox();

        scheduler.Register(new TActorRef(1), mailbox);
    }

    [Fact]
    public void Unregister_NotRegistered_DoesNotThrow()
    {
        using var scheduler = new TInlineScheduler();

        scheduler.Unregister(new TActorRef(42));
    }

    [Fact]
    public async Task QuiesceAsync_NoWork_ReturnsImmediately()
    {
        using var scheduler = new TInlineScheduler();

        await scheduler.QuiesceAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Attach_StoresHost_DoesNotThrow()
    {
        using var scheduler = new TInlineScheduler();
        var host = new FakeSchedulerHost();

        scheduler.Attach(host);
    }

    [Fact]
    public void Attach_CalledTwice_Throws()
    {
        using var scheduler = new TInlineScheduler();
        var host1 = new FakeSchedulerHost();
        var host2 = new FakeSchedulerHost();

        scheduler.Attach(host1);

        Assert.Throws<InvalidOperationException>(() => scheduler.Attach(host2));
    }

    [Fact]
    public void Signal_AfterDispose_Throws()
    {
        var scheduler = new TInlineScheduler();
        scheduler.Dispose();

        Assert.Throws<ObjectDisposedException>(() => scheduler.Signal(new TActorRef(1)));
    }

    [Fact]
    public async Task Signal_AfterAttach_QuiesceCallsRunOneSlice()
    {
        using var scheduler = new TInlineScheduler();
        var host = new FakeSchedulerHost();
        scheduler.Attach(host);

        scheduler.Signal(new TActorRef(7));
        await scheduler.QuiesceAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(1, host.GetRunOneSliceCount(new TActorRef(7)));
    }

    [Fact]
    public async Task Signal_TwiceSameActor_RunOneSliceCalledOnceWhileIdle()
    {
        using var scheduler = new TInlineScheduler();
        var host = new FakeSchedulerHost();
        scheduler.Attach(host);

        scheduler.Signal(new TActorRef(3));
        scheduler.Signal(new TActorRef(3));
        await scheduler.QuiesceAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(1, host.GetRunOneSliceCount(new TActorRef(3)));
    }

    [Fact]
    public async Task Signal_TwoActors_BothRunOneSliceCalled()
    {
        using var scheduler = new TInlineScheduler();
        var host = new FakeSchedulerHost();
        scheduler.Attach(host);

        scheduler.Signal(new TActorRef(1));
        scheduler.Signal(new TActorRef(2));
        await scheduler.QuiesceAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(1, host.GetRunOneSliceCount(new TActorRef(1)));
        Assert.Equal(1, host.GetRunOneSliceCount(new TActorRef(2)));
    }

    [Fact]
    public async Task QuiesceAsync_HostKeepsReportingWork_LoopsUntilIdle()
    {
        using var scheduler = new TInlineScheduler();
        var host = new FakeSchedulerHost();
        host.SetWorkRemaining(new TActorRef(5), runs: 4);
        scheduler.Attach(host);

        scheduler.Signal(new TActorRef(5));
        await scheduler.QuiesceAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(4, host.GetRunOneSliceCount(new TActorRef(5)));
    }

    [Fact]
    public async Task QuiesceAsync_HostNeverIdle_ThrowsTimeout()
    {
        using var scheduler = new TInlineScheduler();
        var host = new FakeSchedulerHost();
        host.SetEndlessWork(new TActorRef(9));
        scheduler.Attach(host);

        scheduler.Signal(new TActorRef(9));

        await Assert.ThrowsAsync<TimeoutException>(
            () => scheduler.QuiesceAsync(TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public async Task QuiesceAsync_Cancelled_ThrowsOperationCancelled()
    {
        using var scheduler = new TInlineScheduler();
        var host = new FakeSchedulerHost();
        host.SetEndlessWork(new TActorRef(11));
        scheduler.Attach(host);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        scheduler.Signal(new TActorRef(11));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => scheduler.QuiesceAsync(TimeSpan.FromSeconds(5), cts.Token));
    }

    [Fact]
    public async Task Signal_DuringRunOneSlice_ProcessedInSameQuiesce()
    {
        using var scheduler = new TInlineScheduler();
        var host = new FakeSchedulerHost();
        host.OnRunOneSlice = (actor, _) =>
        {
            if (actor.SlotIndex == 1 && host.GetRunOneSliceCount(new TActorRef(2)) == 0)
                scheduler.Signal(new TActorRef(2));
        };
        scheduler.Attach(host);

        scheduler.Signal(new TActorRef(1));
        await scheduler.QuiesceAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(1, host.GetRunOneSliceCount(new TActorRef(1)));
        Assert.Equal(1, host.GetRunOneSliceCount(new TActorRef(2)));
    }

    private sealed class FakeMailbox : IMailbox
    {
        public int Count => 0;

        public void Post(object AMessage) { }

        public bool TryReceive(out object? AMessage)
        {
            AMessage = null;
            return false;
        }
    }

    private sealed class FakeSchedulerHost : ISchedulerHost
    {
        private readonly Dictionary<TActorRef, int> FRunCounts = new();
        private readonly Dictionary<TActorRef, int> FWorkRemaining = new();
        private readonly HashSet<TActorRef> FEndless = new();

        public Action<TActorRef, int>? OnRunOneSlice { get; set; }

        public void SetWorkRemaining(TActorRef AActor, int runs)
        {
            FWorkRemaining[AActor] = runs;
        }

        public void SetEndlessWork(TActorRef AActor)
        {
            FEndless.Add(AActor);
        }

        public int GetRunOneSliceCount(TActorRef AActor)
        {
            return FRunCounts.TryGetValue(AActor, out var c) ? c : 0;
        }

        public bool RunOneSlice(TActorRef AActor, int AMaxMessages)
        {
            FRunCounts[AActor] = GetRunOneSliceCount(AActor) + 1;
            OnRunOneSlice?.Invoke(AActor, AMaxMessages);

            if (FEndless.Contains(AActor))
                return true;

            if (FWorkRemaining.TryGetValue(AActor, out var remaining) && remaining > 1)
            {
                FWorkRemaining[AActor] = remaining - 1;
                return true;
            }

            return false;
        }

        public bool IsActorIdle(TActorRef AActor)
        {
            if (FEndless.Contains(AActor))
                return false;

            return !FWorkRemaining.TryGetValue(AActor, out var r) || r <= 1;
        }
    }
}
