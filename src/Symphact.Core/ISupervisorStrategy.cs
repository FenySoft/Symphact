namespace Symphact.Core;

/// <summary>
/// hu: Supervisor stratégia interfész. A szülő aktor stratégiája dönti el, mi történjen egy
/// hibázó gyerek aktorral. Az interfész mögött a CFPU hardveren fix-function restart logika
/// állhat — ezért interfész, nem konkrét osztály.
/// <br />
/// en: Supervisor strategy interface. The parent actor's strategy decides what to do with a
/// failed child actor. Behind this interface, CFPU hardware may implement fixed-function
/// restart logic — hence an interface, not a concrete class.
/// </summary>
public interface ISupervisorStrategy
{
    /// <summary>
    /// hu: Döntés egy gyerek aktor hibájáról. A visszatérési érték határozza meg, hogy a gyerek
    /// folytatódik, újraindul, megáll, vagy a hiba eszkalálódik.
    /// <br />
    /// en: Decide on a child actor's failure. The return value determines whether the child
    /// resumes, restarts, stops, or the failure escalates.
    /// </summary>
    /// <param name="AChild">
    /// hu: A hibázó gyerek aktor referenciája.
    /// <br />
    /// en: Reference to the failed child actor.
    /// </param>
    /// <param name="AException">
    /// hu: A gyerek által dobott kivétel.
    /// <br />
    /// en: The exception thrown by the child.
    /// </param>
    /// <returns>
    /// hu: Az alkalmazandó direktíva.
    /// <br />
    /// en: The directive to apply.
    /// </returns>
    ESupervisorDirective Decide(TActorRef AChild, Exception AException);
}
