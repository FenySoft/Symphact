namespace Symphact.Core;

/// <summary>
/// hu: Aktor ütemező absztrakció (M0.4). Az IScheduler felelős az aktorok kézi vagy automatikus
/// futtatásáért: a TActorSystem signal-eket küld neki ("ennek az aktornak van mit csinálnia"),
/// és a scheduler dönti el, mikor és melyik thread-en hívja vissza a host-ot
/// (TActorSystem.RunOneSlice). Két referencia implementáció létezik:
/// <br />
/// 1. TInlineScheduler — szinkron, single-threaded; a régi Drain-mode belső motorja.
/// 2. TDedicatedThreadScheduler — per-aktor egy .NET Thread; a CFPU "minden aktor dedikált
///    core-on fut" modell szoftveres szimulációja.
/// <br />
/// CFPU megfelelés: HW oldalon nincs scheduler — minden aktor a saját core-ján fut, a Signal HW
/// IRQ-line, a Register/Unregister core boot/reset. A TCfpuScheduler valószínűleg vékony
/// MMIO híd lesz; a Signal-t maga a HW intézi a Send oldalán.
/// <br />
/// Szálbiztonság: minden publikus metódus thread-safe. A Signal allokáció-mentes hot path.
/// <br />
/// en: Actor scheduler abstraction (M0.4). The IScheduler is responsible for running actors
/// either manually or automatically: the TActorSystem sends signals ("this actor has work
/// to do") and the scheduler decides when and on which thread to call back into the host
/// (TActorSystem.RunOneSlice). Two reference implementations exist:
/// <br />
/// 1. TInlineScheduler — synchronous, single-threaded; the inner engine of legacy Drain mode.
/// 2. TDedicatedThreadScheduler — one .NET Thread per actor; the software simulation of the
///    CFPU "every actor runs on a dedicated core" model.
/// <br />
/// CFPU correspondence: on HW there is no scheduler — every actor runs on its dedicated core, Signal is the
/// HW IRQ line, Register/Unregister are core boot/reset. TCfpuScheduler will likely be a
/// thin MMIO bridge; Signal is performed by the hardware itself on Send.
/// <br />
/// Thread-safety: all public methods are thread-safe. Signal is the allocation-free hot path.
/// </summary>
public interface IScheduler : IDisposable
{
    /// <summary>
    /// hu: A scheduler hozzákapcsolása egy futó rendszerhez. Egy schedulerhez egyetlen host
    /// tartozik. Második hívás InvalidOperationException-t dob.
    /// <br />
    /// en: Attach the scheduler to a running system. A scheduler may have at most one host;
    /// a second call throws InvalidOperationException.
    /// </summary>
    /// <param name="AHost">
    /// hu: A host (általában a TActorSystem), amelynek a RunOneSlice / IsActorIdle metódusát
    /// a scheduler visszahívja.
    /// <br />
    /// en: The host (usually the TActorSystem), whose RunOneSlice / IsActorIdle the scheduler
    /// will call back into.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// hu: Ha már korábban hívva volt és a host be van állítva.
    /// <br />
    /// en: If a host was already attached.
    /// </exception>
    void Attach(ISchedulerHost AHost);

    /// <summary>
    /// hu: Új aktor regisztrálása az ütemezőhöz spawn-kor. CFPU oldalon: core boot szekvencia.
    /// <br />
    /// en: Register a new actor with the scheduler at spawn time. CFPU side: core boot sequence.
    /// </summary>
    /// <param name="AActor">
    /// hu: A regisztrálandó aktor referenciája.
    /// <br />
    /// en: Reference of the actor to register.
    /// </param>
    /// <param name="AMailbox">
    /// hu: Az aktor mailboxa — a scheduler szükség esetén jelzéseket olvas innen.
    /// <br />
    /// en: The actor's mailbox — the scheduler may read signals from it as needed.
    /// </param>
    void Register(TActorRef AActor, IMailbox AMailbox);

    /// <summary>
    /// hu: Aktor véglegesen leállt — a scheduler-nek le kell zárnia a kapcsolódó dedikált
    /// thread-et / mailbox várakozást. CFPU oldalon: core reset.
    /// <br />
    /// en: Actor has stopped permanently — the scheduler must shut down any associated
    /// dedicated thread or mailbox wait. CFPU side: core reset.
    /// </summary>
    /// <param name="AActor">
    /// hu: A leállítandó aktor referenciája.
    /// <br />
    /// en: Reference of the actor to unregister.
    /// </param>
    void Unregister(TActorRef AActor);

    /// <summary>
    /// hu: Ébresztő jel — "a megadott aktor mailboxába üzenet érkezett." Allokáció-mentes
    /// hot path. Idempotens: ha az aktor már fut vagy futásra kész, no-op.
    /// <br />
    /// en: Wake-up signal — "a message has arrived for the given actor's mailbox." This is
    /// the allocation-free hot path. Idempotent: if the actor is already running or runnable,
    /// it is a no-op.
    /// </summary>
    /// <param name="AActor">
    /// hu: A jelzendő aktor referenciája.
    /// <br />
    /// en: Reference of the actor being signalled.
    /// </param>
    /// <exception cref="ObjectDisposedException">
    /// hu: Ha a scheduler már le van zárva.
    /// <br />
    /// en: If the scheduler has been disposed.
    /// </exception>
    void Signal(TActorRef AActor);

    /// <summary>
    /// hu: Aszinkron várakozás a quiescence-re — minden mailbox üres és minden ütemezett
    /// munka leállt. Determinisztikus barrier teszt-fixture-ökhöz: ha a megadott időn belül
    /// nem áll be a quiescence, TimeoutException-t dob (nem deadlockol).
    /// <br />
    /// FONTOS: a quiescence detekció pontos mechanikája az implementáció felelőssége. A
    /// TInlineScheduler a saját pending-counter-ét használja; a TDedicatedThreadScheduler
    /// per-thread idle flag-eket; egy hipotetikus CFPU scheduler MMIO core-status regisztereket.
    /// A kontraktus: a hívás visszatérése után minden eddig küldött üzenet feldolgozódott
    /// (vagy a megfelelő aktor le van állítva), és új jelzésig nincs futó munka.
    /// <br />
    /// en: Asynchronous wait for quiescence — every mailbox is empty and every scheduled task
    /// has stopped. Deterministic barrier for test fixtures: if quiescence is not reached
    /// within the given timeout, throws TimeoutException (does not deadlock).
    /// <br />
    /// IMPORTANT: the exact quiescence detection mechanism is the implementation's responsibility.
    /// TInlineScheduler uses its own pending-counter; TDedicatedThreadScheduler uses per-thread
    /// idle flags; a hypothetical CFPU scheduler would use MMIO core-status registers. The
    /// contract: after the call returns, every previously sent message has been processed
    /// (or its target actor is stopped), and no work is in flight until a new signal arrives.
    /// </summary>
    /// <param name="ATimeout">
    /// hu: Maximum várakozási idő. Ha ennyi idő alatt sem áll be a quiescence, TimeoutException.
    /// <br />
    /// en: Maximum wait time. If quiescence is not reached, throws TimeoutException.
    /// </param>
    /// <param name="ACancellationToken">
    /// hu: Megszakítási token. Cancel esetén OperationCanceledException.
    /// <br />
    /// en: Cancellation token. On cancellation throws OperationCanceledException.
    /// </param>
    /// <exception cref="TimeoutException">
    /// hu: Ha az ATimeout lejárt és a rendszer nem ért quiescence-be.
    /// <br />
    /// en: If ATimeout elapsed and the system did not reach quiescence.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// hu: Ha az ACancellationToken jelzett.
    /// <br />
    /// en: If ACancellationToken was signalled.
    /// </exception>
    Task QuiesceAsync(TimeSpan ATimeout, CancellationToken ACancellationToken = default);
}
