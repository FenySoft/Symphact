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
    public void Constructor_CreatesUsableScheduler()
    {
        using var scheduler = new TInlineScheduler();

        // Newly constructed scheduler must be in a state that accepts Attach + Signal.
        Assert.NotNull(scheduler);
        Assert.IsAssignableFrom<IScheduler>(scheduler);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var scheduler = new TInlineScheduler();

        scheduler.Dispose();
        scheduler.Dispose();

        // Second Dispose did not throw; behavior contract: idempotency.
        // Post-Dispose Signal raises ObjectDisposedException — see Signal_AfterDispose_Throws.
        Assert.Throws<ObjectDisposedException>(() => scheduler.Signal(new TActorRef(1)));
    }

    [Fact]
    public async Task Signal_BeforeAttach_DefersUntilAttach()
    {
        using var scheduler = new TInlineScheduler();
        var host = new FakeSchedulerHost();

        // Signal arrives before any host is attached — must not throw; the wakeup is
        // expected to be deferred and replayed at the first Quiesce after Attach.
        scheduler.Signal(new TActorRef(7));
        scheduler.Attach(host);
        await scheduler.QuiesceAsync(TimeSpan.FromSeconds(1));

        // Behavior contract: the pre-Attach Signal IS observed after Attach.
        Assert.Equal(1, host.GetRunOneSliceCount(new TActorRef(7)));
    }

    [Fact]
    public void Register_BeforeAttach_IsAccepted()
    {
        using var scheduler = new TInlineScheduler();
        var mailbox = new FakeMailbox();

        // Register before Attach must be a legal lifecycle ordering — the scheduler
        // accepts it and stores the mailbox association for future RunOneSlice calls.
        scheduler.Register(new TActorRef(1), mailbox);

        // Following Unregister of the same ref also succeeds (round-trip).
        scheduler.Unregister(new TActorRef(1));
    }

    [Fact]
    public void Unregister_UnknownActor_IsNoOp()
    {
        using var scheduler = new TInlineScheduler();

        // Unregister of a never-registered ref is a silent no-op (the contract makes
        // the caller's life easier on actor stop racing against pending signals).
        scheduler.Unregister(new TActorRef(42));

        // Scheduler remains usable afterwards.
        scheduler.Unregister(new TActorRef(42));
    }

    [Fact]
    public async Task QuiesceAsync_NoWork_ReturnsImmediately()
    {
        using var scheduler = new TInlineScheduler();
        var host = new FakeSchedulerHost();
        scheduler.Attach(host);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await scheduler.QuiesceAsync(TimeSpan.FromSeconds(1));
        stopwatch.Stop();

        // Empty queue must return promptly — not spin until the timeout.
        Assert.True(
            stopwatch.ElapsedMilliseconds < 200,
            $"QuiesceAsync on empty queue took {stopwatch.ElapsedMilliseconds} ms (expected < 200 ms)");
        Assert.Equal(0, host.GetRunOneSliceCount(new TActorRef(1)));
    }

    [Fact]
    public void Attach_StoresHostForLaterCallbacks()
    {
        using var scheduler = new TInlineScheduler();
        var host = new FakeSchedulerHost();

        scheduler.Attach(host);

        // Behavior contract: a Signal after Attach must reach the host. (Detailed
        // round-trip in Signal_AfterAttach_QuiesceCallsRunOneSlice.) Here we just
        // ensure Attach + Signal does not throw on the happy path.
        scheduler.Signal(new TActorRef(1));
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
