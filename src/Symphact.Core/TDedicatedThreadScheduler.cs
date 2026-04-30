namespace Symphact.Core;

/// <summary>
/// hu: Per-aktor egy .NET Thread ütemező (M0.4) — a CFPU "minden core fizikailag egy aktor"
/// modell szoftveres szimulációja. Minden Register egy új OS-thread-et indít, amely az
/// aktorhoz tartozó IMailboxSignal-on blokkolódik. Signal érkezésekor a thread a host
/// RunOneSlice metódusát hívja, majd visszaüvegez a Wait-be.
/// <br />
/// Single-threaded actor invariáns: trivializálódik — egy aktorhoz egy thread tartozik,
/// így a handler csak ezen az 1 thread-en futhat. Cross-actor communication a TActorSystem
/// thread-safe Send-jén keresztül.
/// <br />
/// CFPU megfelelés: ez a leghasznosabb fejlesztői platform a CFPU programozási modell
/// elsajátításához. A HW oldalon a Register = core boot, a Signal = mailbox-IRQ asserted,
/// a Wait = WFI instrukció. CFPU-n maga a scheduler eltűnik, mert minden core önmagát futtatja.
/// <br />
/// Default hard cap: 1000 aktor (1000 thread × 1 MB stack ≈ 1 GiB virtual memory). Az OS
/// scheduler 1000 thread fölött az interleaving overhead miatt is megfojthatja a teljesítményt.
/// Production multi-actor szcenárióhoz lásd később M2.2 — de M0.4-ben a TDedicatedThreadScheduler
/// a CFPU-rebő szimuláció eszköze, NEM production load-balancer.
/// <br />
/// en: One .NET Thread per actor scheduler (M0.4) — software simulation of the CFPU "each
/// core is physically one actor" model. Every Register starts a new OS thread, blocking on
/// the actor's IMailboxSignal. On Signal, the thread calls the host's RunOneSlice and then
/// returns to Wait.
/// <br />
/// Single-threaded actor invariant: trivially holds — one thread per actor means the handler
/// can only run on that single thread. Cross-actor communication uses TActorSystem's
/// thread-safe Send.
/// <br />
/// CFPU correspondence: this is the most useful developer platform for learning the CFPU
/// programming model. On HW, Register = core boot, Signal = mailbox IRQ asserted, Wait = WFI
/// instruction. On CFPU the scheduler vanishes, because each core runs itself.
/// <br />
/// Default hard cap: 1000 actors (1000 threads × 1 MB stack ≈ 1 GiB virtual memory). The OS
/// scheduler may also choke on interleaving overhead beyond 1000 threads. For production
/// multi-actor scenarios see future M2.2 — TDedicatedThreadScheduler in M0.4 is the CFPU
/// simulation tool, NOT a production load-balancer.
/// </summary>
public sealed class TDedicatedThreadScheduler : IScheduler
{
    /// <summary>
    /// hu: Alapértelmezett aktor-felső korlát.
    /// <br />
    /// en: Default upper bound on the number of actors.
    /// </summary>
    public const int CDefaultMaxActors = 1000;

    private const string CNoHostError =
        "TDedicatedThreadScheduler has no attached host. Call Attach first.";

    private readonly IMailboxSignalProvider FSignalProvider;
    private readonly int FMaxActors;
    private readonly Dictionary<TActorRef, TActorThread> FThreads = new();
    private readonly object FLock = new();
    private ISchedulerHost? FHost;
    private volatile bool FDisposed;

    /// <summary>
    /// hu: Új ütemezőt hoz létre a megadott platformmal és aktor-felső korláttal.
    /// <br />
    /// en: Constructs a scheduler with the given platform and actor cap.
    /// </summary>
    /// <param name="APlatform">
    /// hu: Az IPlatform implementáció — kötelezően implementálnia kell az
    /// IMailboxSignalProvider-t is (a TDotNetPlatform pl. mindkettőt).
    /// <br />
    /// en: The IPlatform implementation — must also implement IMailboxSignalProvider
    /// (e.g. TDotNetPlatform implements both).
    /// </param>
    /// <param name="AMaxActors">
    /// hu: Aktor-felső korlát (default: 1000). Túllépéskor Register InvalidOperationException-t dob.
    /// <br />
    /// en: Upper bound on actors (default: 1000). Register throws InvalidOperationException
    /// when exceeded.
    /// </param>
    public TDedicatedThreadScheduler(IPlatform APlatform, int AMaxActors = CDefaultMaxActors)
    {
        ArgumentNullException.ThrowIfNull(APlatform);

        if (APlatform is not IMailboxSignalProvider provider)
            throw new ArgumentException(
                $"Platform '{APlatform.GetType().Name}' must implement IMailboxSignalProvider " +
                "to be used with TDedicatedThreadScheduler.",
                nameof(APlatform));

        if (AMaxActors <= 0)
            throw new ArgumentOutOfRangeException(nameof(AMaxActors),
                AMaxActors,
                "AMaxActors must be positive.");

        FSignalProvider = provider;
        FMaxActors = AMaxActors;
    }

    /// <inheritdoc />
    public void Attach(ISchedulerHost AHost)
    {
        ArgumentNullException.ThrowIfNull(AHost);
        ThrowIfDisposed();

        lock (FLock)
        {
            if (FHost is not null)
                throw new InvalidOperationException(
                    "TDedicatedThreadScheduler already has an attached host.");

            FHost = AHost;
        }
    }

    /// <inheritdoc />
    public void Register(TActorRef AActor, IMailbox AMailbox)
    {
        ArgumentNullException.ThrowIfNull(AMailbox);
        ThrowIfDisposed();

        TActorThread newThread;

        lock (FLock)
        {
            if (FHost is null)
                throw new InvalidOperationException(CNoHostError);

            if (FThreads.ContainsKey(AActor))
                throw new InvalidOperationException(
                    $"Actor {AActor} is already registered.");

            if (FThreads.Count >= FMaxActors)
                throw new InvalidOperationException(
                    $"TDedicatedThreadScheduler reached its hard cap of {FMaxActors} actors. " +
                    "For higher actor counts use TInlineScheduler or wait for M2.2 scheduler-actor.");

            var signal = FSignalProvider.CreateSignal(AMailbox)
                ?? throw new InvalidOperationException(
                    "IMailboxSignalProvider returned null; polling fallback is not yet implemented.");

            newThread = new TActorThread(AActor, signal, FHost);
            FThreads[AActor] = newThread;
        }

        newThread.Start();
    }

    /// <inheritdoc />
    public void Unregister(TActorRef AActor)
    {
        ThrowIfDisposed();

        TActorThread? toStop;

        lock (FLock)
        {
            if (!FThreads.TryGetValue(AActor, out toStop))
                return;

            FThreads.Remove(AActor);
        }

        toStop.RequestStop();
        toStop.Join(TimeSpan.FromSeconds(5));
        toStop.Dispose();
    }

    /// <inheritdoc />
    public void Signal(TActorRef AActor)
    {
        ThrowIfDisposed();

        TActorThread? thread;

        lock (FLock)
        {
            if (FHost is null)
                throw new InvalidOperationException(CNoHostError);

            if (!FThreads.TryGetValue(AActor, out thread))
                return;
        }

        thread.NotifySignal();
    }

    /// <inheritdoc />
    public Task QuiesceAsync(TimeSpan ATimeout, CancellationToken ACancellationToken = default)
    {
        ThrowIfDisposed();
        ACancellationToken.ThrowIfCancellationRequested();

        if (FHost is null)
            return Task.CompletedTask;

        var deadline = DateTime.UtcNow + ATimeout;

        // Two-phase quiescence: sweep across all threads twice in a row, both reporting
        // idle. This guards against the race where one thread is between Wait-return and
        // RunOneSlice-start while another thread sees "everyone idle".
        var consecutiveIdleSweeps = 0;

        while (true)
        {
            ACancellationToken.ThrowIfCancellationRequested();

            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException(
                    $"TDedicatedThreadScheduler.QuiesceAsync did not converge within {ATimeout}.");

            var allIdle = true;

            TActorThread[] snapshot;

            lock (FLock)
            {
                snapshot = FThreads.Values.ToArray();
            }

            foreach (var thread in snapshot)
            {
                if (!thread.IsBlockedOnWait || !FHost.IsActorIdle(thread.Actor))
                {
                    allIdle = false;
                    break;
                }
            }

            if (allIdle)
            {
                consecutiveIdleSweeps++;

                if (consecutiveIdleSweeps >= 2)
                    return Task.CompletedTask;
            }
            else
            {
                consecutiveIdleSweeps = 0;
            }

            Thread.Sleep(1);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (FDisposed)
            return;

        FDisposed = true;

        TActorThread[] all;

        lock (FLock)
        {
            all = FThreads.Values.ToArray();
            FThreads.Clear();
            FHost = null;
        }

        foreach (var thread in all)
            thread.RequestStop();

        foreach (var thread in all)
        {
            thread.Join(TimeSpan.FromSeconds(5));
            thread.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (FDisposed)
            throw new ObjectDisposedException(nameof(TDedicatedThreadScheduler));
    }

    private sealed class TActorThread : IDisposable
    {
        private readonly Thread FThread;
        private readonly IMailboxSignal FSignal;
        private readonly ISchedulerHost FHost;
        private readonly CancellationTokenSource FCts;
        private volatile bool FBlockedOnWait;

        public TActorThread(TActorRef AActor, IMailboxSignal ASignal, ISchedulerHost AHost)
        {
            Actor = AActor;
            FSignal = ASignal;
            FHost = AHost;
            FCts = new CancellationTokenSource();
            FThread = new Thread(Run)
            {
                Name = $"Symphact-Actor-{AActor.SlotIndex}",
                IsBackground = true
            };
        }

        public TActorRef Actor { get; }

        public bool IsBlockedOnWait => FBlockedOnWait;

        public void Start() => FThread.Start();

        public void NotifySignal() => FSignal.NotifyMessageArrived();

        public void RequestStop()
        {
            FCts.Cancel();
            FSignal.NotifyMessageArrived();
        }

        public bool Join(TimeSpan ATimeout) => FThread.Join(ATimeout);

        public void Dispose()
        {
            FCts.Dispose();
            FSignal.Dispose();
        }

        private void Run()
        {
            var token = FCts.Token;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    FBlockedOnWait = true;
                    FSignal.Wait(token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                finally
                {
                    FBlockedOnWait = false;
                }

                if (token.IsCancellationRequested)
                    return;

                FHost.RunOneSlice(Actor, 0);
            }
        }
    }
}
