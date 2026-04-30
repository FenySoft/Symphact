using Symphact.Core;
using Symphact.Core.Tests.Helpers;

namespace Symphact.Core.Tests;

/// <summary>
/// hu: A TDedicatedThreadScheduler tesztjei (M0.4 — C.6). Per-aktor egy .NET Thread, a
/// CFPU "minden core fizikailag egy aktor" modell szoftveres szimulációja. A handler
/// nem a hívó thread-jén, hanem a dedikált aktor-thread-en fut.
/// <br />
/// en: Tests for TDedicatedThreadScheduler (M0.4 — C.6). One .NET Thread per actor — the
/// software simulation of the CFPU "each core is physically one actor" model. The handler
/// runs on the dedicated actor thread, not on the caller's thread.
/// </summary>
public sealed class TDedicatedThreadSchedulerTests
{
    private sealed class TThreadCapturingHost : ISchedulerHost
    {
        private readonly Latch FCompletion;
        public int? FirstSliceThreadId { get; private set; }
        public List<int> AllThreadIds { get; } = new();

        public TThreadCapturingHost(Latch ACompletion)
        {
            FCompletion = ACompletion;
        }

        public bool RunOneSlice(TActorRef AActor, int AMaxMessages)
        {
            FirstSliceThreadId ??= Environment.CurrentManagedThreadId;

            lock (AllThreadIds)
            {
                AllThreadIds.Add(Environment.CurrentManagedThreadId);
            }

            FCompletion.Signal();
            return false;
        }

        public bool IsActorIdle(TActorRef AActor) => true;
    }

    [Fact]
    public void Constructor_DoesNotThrow()
    {
        using var scheduler = new TDedicatedThreadScheduler(new TDotNetPlatform());

        Assert.NotNull(scheduler);
    }

    [Fact]
    public void Constructor_NullSignalProvider_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TDedicatedThreadScheduler(null!));
    }

    [Fact]
    public void Constructor_PlatformWithoutSignalProvider_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new TDedicatedThreadScheduler(new TFakePlatformNoSignal()));
    }

    [Fact]
    public void Register_AfterAttach_StartsActorThread()
    {
        using var latch = new Latch(1);
        var host = new TThreadCapturingHost(latch);
        using var scheduler = new TDedicatedThreadScheduler(new TDotNetPlatform());
        scheduler.Attach(host);
        var mailbox = new TMailbox();

        scheduler.Register(new TActorRef(1), mailbox);
        scheduler.Signal(new TActorRef(1));

        latch.AssertCompleted(TimeSpan.FromSeconds(2));
        Assert.NotNull(host.FirstSliceThreadId);
        Assert.NotEqual(Environment.CurrentManagedThreadId, host.FirstSliceThreadId);
    }

    [Fact]
    public void Signal_TwoActors_RunOnDifferentThreads()
    {
        using var latch = new Latch(2);
        var host = new TThreadCapturingHost(latch);
        using var scheduler = new TDedicatedThreadScheduler(new TDotNetPlatform());
        scheduler.Attach(host);

        scheduler.Register(new TActorRef(1), new TMailbox());
        scheduler.Register(new TActorRef(2), new TMailbox());

        scheduler.Signal(new TActorRef(1));
        scheduler.Signal(new TActorRef(2));

        latch.AssertCompleted(TimeSpan.FromSeconds(2));
        Assert.Equal(2, host.AllThreadIds.Count);
        Assert.NotEqual(host.AllThreadIds[0], host.AllThreadIds[1]);
    }

    [Fact]
    public void Unregister_StopsActorThread()
    {
        using var latch = new Latch(1);
        var host = new TThreadCapturingHost(latch);
        using var scheduler = new TDedicatedThreadScheduler(new TDotNetPlatform());
        scheduler.Attach(host);
        scheduler.Register(new TActorRef(1), new TMailbox());

        scheduler.Signal(new TActorRef(1));
        latch.AssertCompleted(TimeSpan.FromSeconds(2));

        // Unregister-nek a thread-et 2 másodpercen belül le kell zárnia
        scheduler.Unregister(new TActorRef(1));
        // No deadlock = test passes; if join hangs, dotnet test --blame-hang catches it.
    }

    [Fact]
    public void Dispose_StopsAllThreads()
    {
        var host = new TThreadCapturingHost(new Latch(1));
        var scheduler = new TDedicatedThreadScheduler(new TDotNetPlatform());
        scheduler.Attach(host);

        scheduler.Register(new TActorRef(1), new TMailbox());
        scheduler.Register(new TActorRef(2), new TMailbox());
        scheduler.Register(new TActorRef(3), new TMailbox());

        scheduler.Dispose();
        // Sikeres Dispose = minden thread joinolva, semmi nem ragad.
    }

    [Fact]
    public void Register_OverDefaultMaxActors_Throws()
    {
        using var scheduler = new TDedicatedThreadScheduler(
            new TDotNetPlatform(),
            AMaxActors: 2);
        var host = new TThreadCapturingHost(new Latch(1));
        scheduler.Attach(host);

        scheduler.Register(new TActorRef(1), new TMailbox());
        scheduler.Register(new TActorRef(2), new TMailbox());

        Assert.Throws<InvalidOperationException>(() =>
            scheduler.Register(new TActorRef(3), new TMailbox()));
    }

    [Fact]
    public void Register_DuplicateActorRef_Throws()
    {
        using var scheduler = new TDedicatedThreadScheduler(new TDotNetPlatform());
        var host = new TThreadCapturingHost(new Latch(1));
        scheduler.Attach(host);

        scheduler.Register(new TActorRef(1), new TMailbox());

        Assert.Throws<InvalidOperationException>(() =>
            scheduler.Register(new TActorRef(1), new TMailbox()));
    }

    [Fact]
    public void Signal_BeforeAttach_Throws()
    {
        using var scheduler = new TDedicatedThreadScheduler(new TDotNetPlatform());

        Assert.Throws<InvalidOperationException>(() =>
            scheduler.Signal(new TActorRef(1)));
    }

    [Fact]
    public void Attach_CalledTwice_Throws()
    {
        using var scheduler = new TDedicatedThreadScheduler(new TDotNetPlatform());
        var host1 = new TThreadCapturingHost(new Latch(1));
        var host2 = new TThreadCapturingHost(new Latch(1));

        scheduler.Attach(host1);
        Assert.Throws<InvalidOperationException>(() => scheduler.Attach(host2));
    }

    private sealed class TFakePlatformNoSignal : IPlatform
    {
        public IMailbox CreateMailbox() => new TMailbox();
    }
}
