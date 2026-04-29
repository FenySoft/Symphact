namespace Symphact.Core;

/// <summary>
/// hu: A supervisor stratégia által visszaadott utasítás, ami meghatározza, mi történjen egy
/// hibázó gyerek aktorral. A négy lehetőség az Erlang/OTP modell alapján:
/// Resume (folytatás azonos állapottal), Restart (újraindítás Init-tel), Stop (leállítás),
/// Escalate (hiba továbbítása a szülő felé).
/// <br />
/// en: The directive returned by a supervisor strategy, determining what happens to a failed
/// child actor. The four options follow the Erlang/OTP model: Resume (continue with same state),
/// Restart (reinitialise via Init), Stop (terminate), Escalate (propagate failure to parent).
/// </summary>
public enum ESupervisorDirective
{
    /// <summary>
    /// hu: Az aktor folytatja a működést a hibát megelőző állapottal. A hibás üzenet elveszik.
    /// <br />
    /// en: The actor resumes with the state before the failure. The failing message is lost.
    /// </summary>
    Resume = 0,

    /// <summary>
    /// hu: Az aktor újraindul: Init() hívódik, friss állapottal folytatja. A mailbox megmarad.
    /// <br />
    /// en: The actor restarts: Init() is called, continues with fresh state. Mailbox is preserved.
    /// </summary>
    Restart = 1,

    /// <summary>
    /// hu: Az aktor véglegesen leáll. PostStop hívódik, további üzenetek eldobódnak.
    /// <br />
    /// en: The actor is permanently stopped. PostStop is called, further messages are dropped.
    /// </summary>
    Stop = 2,

    /// <summary>
    /// hu: A hiba eszkalálódik a szülő aktor felé — mintha a szülő maga dobta volna a kivételt.
    /// <br />
    /// en: The failure escalates to the parent actor — as if the parent itself had thrown.
    /// </summary>
    Escalate = 3
}
