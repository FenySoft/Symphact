using Symphact.Core;
using Symphact.Platform.DotNet;

namespace Symphact.Platform.DotNet.Tests;

/// <summary>
/// hu: A TDotNetMailboxSignal tesztjei (M0.4 — C.5). Az IMailboxSignal szinkron Wait-tel
/// működik (CFPU WFI-kompatibilis, zero Task allokáció). A jelzés latching: ha Notify
/// előbb történik mint Wait, a következő Wait azonnal visszatér.
/// <br />
/// en: Tests for TDotNetMailboxSignal (M0.4 — C.5). The IMailboxSignal uses synchronous Wait
/// (CFPU WFI-compatible, zero Task allocation). Signalling is latching: if Notify happens
/// before Wait, the next Wait returns immediately.
/// </summary>
public sealed class TDotNetMailboxSignalTests
{
    [Fact]
    public void Constructor_DoesNotThrow()
    {
        using var signal = new TDotNetMailboxSignal();

        Assert.NotNull(signal);
    }

    [Fact]
    public void Notify_BeforeWait_LatchesAndReturnsImmediately()
    {
        using var signal = new TDotNetMailboxSignal();

        signal.NotifyMessageArrived();

        // Ha a latching jól működik, a Wait nem blokkol
        signal.Wait(CancellationToken.None);
    }

    [Fact]
    public void Wait_AfterNotify_OnlyConsumedOnce()
    {
        using var signal = new TDotNetMailboxSignal();
        signal.NotifyMessageArrived();

        signal.Wait(CancellationToken.None);

        // Második Wait blokkolódna 100 ms-ig — Cancellation tokenel rövidre vágjuk.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        Assert.Throws<OperationCanceledException>(() => signal.Wait(cts.Token));
    }

    [Fact]
    public void Wait_BeforeNotify_BlocksThenReturns()
    {
        using var signal = new TDotNetMailboxSignal();
        var waitCompleted = false;
        var waitThread = new Thread(() =>
        {
            signal.Wait(CancellationToken.None);
            waitCompleted = true;
        });

        waitThread.Start();
        Thread.Sleep(20);

        Assert.False(waitCompleted);

        signal.NotifyMessageArrived();
        Assert.True(waitThread.Join(TimeSpan.FromSeconds(1)));
        Assert.True(waitCompleted);
    }

    [Fact]
    public void Wait_Cancellation_Throws()
    {
        using var signal = new TDotNetMailboxSignal();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => signal.Wait(cts.Token));
    }

    [Fact]
    public void Wait_DelayedCancellation_Throws()
    {
        using var signal = new TDotNetMailboxSignal();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        Assert.Throws<OperationCanceledException>(() => signal.Wait(cts.Token));
    }

    [Fact]
    public void Notify_MultipleTimes_OnlyOneWaitConsumes()
    {
        using var signal = new TDotNetMailboxSignal();

        signal.NotifyMessageArrived();
        signal.NotifyMessageArrived();
        signal.NotifyMessageArrived();

        signal.Wait(CancellationToken.None);

        // A többszörös Notify egyszerre latch-el — csak egy Wait fogyasztja
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        Assert.Throws<OperationCanceledException>(() => signal.Wait(cts.Token));
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var signal = new TDotNetMailboxSignal();

        signal.Dispose();
        signal.Dispose();
    }

    [Fact]
    public void Wait_AfterDispose_Throws()
    {
        var signal = new TDotNetMailboxSignal();
        signal.Dispose();

        Assert.Throws<ObjectDisposedException>(() => signal.Wait(CancellationToken.None));
    }

    [Fact]
    public void Notify_AfterDispose_NoOp()
    {
        var signal = new TDotNetMailboxSignal();
        signal.Dispose();

        // Notify after dispose: no exception, no observable effect.
        signal.NotifyMessageArrived();
    }

    [Fact]
    public void Platform_ImplementsSignalProvider()
    {
        IPlatform platform = new TDotNetPlatform();

        Assert.IsAssignableFrom<IMailboxSignalProvider>(platform);
    }

    [Fact]
    public void Platform_CreateSignal_ReturnsValidSignal()
    {
        var platform = new TDotNetPlatform();
        var mailbox = platform.CreateMailbox();

        var signal = ((IMailboxSignalProvider)platform).CreateSignal(mailbox);

        Assert.NotNull(signal);
        signal.Dispose();
    }
}
