namespace Symphact.Core;

/// <summary>
/// hu: Szinkron, single-threaded scheduler (M0.4). A Signal egy belső "futnia kell"
/// queue-ba teszi az aktort (idempotens, lock alatt); a tényleges feldolgozás csak a
/// QuiesceAsync hívásakor történik, szigorúan szekvenciálisan, a hívó thread-jén.
/// Nincs ütemező thread — a determinisztikus fejlesztői élmény és a régi Drain-mode
/// szemantika (single-host, single-threaded futtatás) garantált.
/// <br />
/// Szálbiztosság: a Signal és a Register/Unregister több thread-ből hívható (a Send a
/// hívó thread-jén fut), de a QuiesceAsync csak egyetlen thread-ből hívható egyszerre.
/// Az alapértelmezett TActorSystem konstruktor ezt a schedulert használja.
/// <br />
/// CFPU megfelelés: nem mappal HW-re — single-host fejlesztői és teszt eszköz.
/// Production- és multi-actor szcenárióhoz a TDedicatedThreadScheduler-t használd.
/// <br />
/// en: Synchronous, single-threaded scheduler (M0.4). Signal pushes the actor onto an
/// internal "runnable" queue (idempotent, under lock); actual processing happens only
/// when QuiesceAsync is called, strictly sequentially, on the caller's thread. There is
/// no scheduler thread — the deterministic developer experience and the legacy Drain-mode
/// semantics (single-host, single-threaded execution) are guaranteed.
/// <br />
/// Thread-safety: Signal, Register, and Unregister may be called from multiple threads
/// (Send runs on the caller's thread), but QuiesceAsync may run on only one thread at a
/// time. This is the scheduler used by the default TActorSystem constructor.
/// <br />
/// CFPU correspondence: does not map to HW — single-host developer and test tool. For
/// production and multi-actor scenarios, use TDedicatedThreadScheduler.
/// </summary>
public sealed class TInlineScheduler : IScheduler
{
    private const int CDefaultMaxMessagesPerSlice = 0;

    private readonly Queue<TActorRef> FRunnable = new();
    private readonly HashSet<TActorRef> FRunnableSet = new();
    private readonly object FRunnableLock = new();
    private ISchedulerHost? FHost;
    private bool FDisposed;

    /// <inheritdoc />
    public void Attach(ISchedulerHost AHost)
    {
        ArgumentNullException.ThrowIfNull(AHost);
        ThrowIfDisposed();

        if (FHost is not null)
            throw new InvalidOperationException(
                "TInlineScheduler already has an attached host; a scheduler may have at most one host.");

        FHost = AHost;
    }

    /// <inheritdoc />
    public void Register(TActorRef AActor, IMailbox AMailbox)
    {
        ArgumentNullException.ThrowIfNull(AMailbox);
        ThrowIfDisposed();
    }

    /// <inheritdoc />
    public void Unregister(TActorRef AActor)
    {
        ThrowIfDisposed();

        lock (FRunnableLock)
        {
            FRunnableSet.Remove(AActor);
        }
    }

    /// <inheritdoc />
    public void Signal(TActorRef AActor)
    {
        ThrowIfDisposed();

        lock (FRunnableLock)
        {
            if (FRunnableSet.Add(AActor))
                FRunnable.Enqueue(AActor);
        }
    }

    /// <inheritdoc />
    public Task QuiesceAsync(TimeSpan ATimeout, CancellationToken ACancellationToken = default)
    {
        ThrowIfDisposed();
        ACancellationToken.ThrowIfCancellationRequested();

        if (FHost is null)
            return Task.CompletedTask;

        var deadline = DateTime.UtcNow + ATimeout;

        while (true)
        {
            ACancellationToken.ThrowIfCancellationRequested();

            TActorRef actor;

            lock (FRunnableLock)
            {
                if (FRunnable.Count == 0)
                    return Task.CompletedTask;

                actor = FRunnable.Dequeue();
                FRunnableSet.Remove(actor);
            }

            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException(
                    $"TInlineScheduler.QuiesceAsync did not converge within {ATimeout}. " +
                    "Possible cause: an actor's host slice keeps reporting work.");

            var didWork = FHost.RunOneSlice(actor, CDefaultMaxMessagesPerSlice);

            if (didWork || !FHost.IsActorIdle(actor))
            {
                lock (FRunnableLock)
                {
                    if (FRunnableSet.Add(actor))
                        FRunnable.Enqueue(actor);
                }
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (FDisposed)
            return;

        FDisposed = true;
        FHost = null;

        lock (FRunnableLock)
        {
            FRunnable.Clear();
            FRunnableSet.Clear();
        }
    }

    private void ThrowIfDisposed()
    {
        if (FDisposed)
            throw new ObjectDisposedException(nameof(TInlineScheduler));
    }
}
