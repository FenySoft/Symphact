using Symphact.Core;

namespace Symphact.Platform.DotNet;

/// <summary>
/// hu: Az IMailboxSignal .NET referencia implementációja (M0.4). AutoResetEvent-tel
/// működik: a NotifyMessageArrived → Set, a Wait → WaitOne. Latching az AutoResetEvent
/// természetéből adódik: ha Set fut Wait előtt, a következő Wait azonnal visszatér.
/// Cancellation a WaitHandle.WaitAny-vel — a token belső WaitHandle-ját is figyeli,
/// így a CancellationTokenSource.Cancel a Wait-ot azonnal kibontja.
/// <br />
/// Szálbiztosság: AutoResetEvent thread-safe. A FDisposed flag volatile a publikációhoz.
/// <br />
/// en: .NET reference implementation of IMailboxSignal (M0.4). Backed by AutoResetEvent:
/// NotifyMessageArrived → Set, Wait → WaitOne. Latching follows from AutoResetEvent
/// semantics: if Set runs before Wait, the next Wait returns immediately. Cancellation
/// uses WaitHandle.WaitAny — it also watches the token's internal WaitHandle, so
/// CancellationTokenSource.Cancel unblocks Wait immediately.
/// <br />
/// Thread-safety: AutoResetEvent is thread-safe. The FDisposed flag is volatile for
/// publication.
/// </summary>
public sealed class TDotNetMailboxSignal : IMailboxSignal
{
    private readonly AutoResetEvent FEvent = new(initialState: false);
    private volatile bool FDisposed;

    /// <inheritdoc />
    public void Wait(CancellationToken ACancellationToken)
    {
        if (FDisposed)
            throw new ObjectDisposedException(nameof(TDotNetMailboxSignal));

        ACancellationToken.ThrowIfCancellationRequested();

        if (!ACancellationToken.CanBeCanceled)
        {
            FEvent.WaitOne();
            return;
        }

        var handles = new[] { FEvent, ACancellationToken.WaitHandle };
        var index = WaitHandle.WaitAny(handles);

        if (index == 1)
            ACancellationToken.ThrowIfCancellationRequested();
    }

    /// <inheritdoc />
    public void NotifyMessageArrived()
    {
        if (FDisposed)
            return;

        FEvent.Set();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (FDisposed)
            return;

        FDisposed = true;
        FEvent.Dispose();
    }
}
