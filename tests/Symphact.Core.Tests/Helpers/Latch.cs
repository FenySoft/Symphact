namespace Symphact.Core.Tests.Helpers;

/// <summary>
/// hu: Egyszerű countdown teszt-segéd timeout-tal — multi-thread tesztek deadlock helyett
/// időtúllépéssel buknak. Minden multi-thread tesztben "using var latch = new Latch(N)"
/// mintát követünk; ha az N esemény nem történik meg WaitWithTimeout idő alatt, a teszt
/// explicit Assert.Fail-lel bukik (nem hangol végtelenig).
/// <br />
/// en: Simple countdown test helper with timeout — multi-thread tests fail by timeout
/// rather than deadlocking. Every multi-thread test follows the "using var latch = new Latch(N)"
/// pattern; if N events do not occur within WaitWithTimeout, the test fails explicitly with
/// Assert.Fail (does not hang indefinitely).
/// </summary>
public sealed class Latch : IDisposable
{
    private readonly CountdownEvent FCountdown;

    public Latch(int ACount)
    {
        FCountdown = new CountdownEvent(ACount);
    }

    public void Signal() => FCountdown.Signal();

    public bool WaitWithTimeout(TimeSpan ATimeout) => FCountdown.Wait(ATimeout);

    public void AssertCompleted(TimeSpan ATimeout)
    {
        if (!FCountdown.Wait(ATimeout))
            Assert.Fail($"Latch did not complete within {ATimeout} (remaining: {FCountdown.CurrentCount}).");
    }

    public void Dispose() => FCountdown.Dispose();
}
