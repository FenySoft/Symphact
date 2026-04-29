namespace Symphact.Core;

/// <summary>
/// hu: One-for-one supervisor stratégia: csak a hibázó gyerek aktort érinti a döntés,
/// a testvérei zavartalanul futnak tovább. Egy opcionális decider függvénnyel konfigurálható,
/// ami exception típus alapján dönt. Ha nincs megadva decider, az alapértelmezett döntés: Restart.
/// <br />
/// en: One-for-one supervisor strategy: only the failed child actor is affected by the decision,
/// its siblings continue undisturbed. Configurable with an optional decider function that decides
/// based on exception type. If no decider is provided, the default decision is: Restart.
/// </summary>
public sealed class TOneForOneStrategy : ISupervisorStrategy
{
    private readonly Func<Exception, ESupervisorDirective> FDecider;

    /// <summary>
    /// hu: Létrehozás alapértelmezett decider-rel (mindig Restart).
    /// <br />
    /// en: Create with default decider (always Restart).
    /// </summary>
    public TOneForOneStrategy()
    {
        FDecider = _ => ESupervisorDirective.Restart;
    }

    /// <summary>
    /// hu: Létrehozás egyedi decider függvénnyel.
    /// <br />
    /// en: Create with a custom decider function.
    /// </summary>
    /// <param name="ADecider">
    /// hu: A döntéshozó függvény. Nem lehet null.
    /// <br />
    /// en: The decider function. Must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// hu: Ha az ADecider null.
    /// <br />
    /// en: If ADecider is null.
    /// </exception>
    public TOneForOneStrategy(Func<Exception, ESupervisorDirective> ADecider)
    {
        ArgumentNullException.ThrowIfNull(ADecider);
        FDecider = ADecider;
    }

    /// <inheritdoc />
    public ESupervisorDirective Decide(TActorRef AChild, Exception AException)
    {
        return FDecider(AException);
    }
}
