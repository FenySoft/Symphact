namespace Symphact.Core;

/// <summary>
/// hu: All-for-one supervisor stratégia: ha bármelyik gyerek aktor hibázik, a direktíva az
/// összes testvérre is érvényes (a hibázó gyerekkel együtt). Egy opcionális decider
/// függvénnyel konfigurálható, ami exception típus alapján dönt. Ha nincs megadva decider,
/// az alapértelmezett döntés: Restart.
/// <br />
/// en: All-for-one supervisor strategy: when any child actor fails, the directive applies to
/// all siblings as well (including the failed child). Configurable with an optional decider
/// function that decides based on exception type. If no decider is provided, the default
/// decision is: Restart.
/// </summary>
public sealed class TAllForOneStrategy : ISupervisorStrategy
{
    private readonly Func<Exception, ESupervisorDirective> FDecider;

    /// <summary>
    /// hu: Létrehozás alapértelmezett decider-rel (mindig Restart).
    /// <br />
    /// en: Create with default decider (always Restart).
    /// </summary>
    public TAllForOneStrategy()
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
    public TAllForOneStrategy(Func<Exception, ESupervisorDirective> ADecider)
    {
        ArgumentNullException.ThrowIfNull(ADecider);
        FDecider = ADecider;
    }

    /// <inheritdoc />
    public bool AffectsAllSiblings => true;

    /// <inheritdoc />
    public ESupervisorDirective Decide(TActorRef AChild, Exception AException)
    {
        return FDecider(AException);
    }
}
