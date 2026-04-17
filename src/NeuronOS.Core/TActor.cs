namespace NeuronOS.Core;

/// <summary>
/// hu: Egy aktor absztrakt alaposztálya. Az aktornak egyetlen állapotú (TState típusú) belső
/// állapota van, amit az Init ad vissza spawn-kor, és a Handle transzformál üzenetről
/// üzenetre. Az állapot tipikusan immutable record type — így a tranzíció determinisztikus,
/// és a régi állapot megmarad a replay-elhető log-ban.
/// <br />
/// en: Abstract base class for an actor. The actor has a single state of type TState, returned
/// by Init at spawn time and transformed message-by-message by Handle. The state is typically
/// an immutable record type — this makes transitions deterministic and preserves the old state
/// in a replayable log.
/// </summary>
/// <typeparam name="TState">
/// hu: Az aktor állapot típusa. Ajánlottan immutable record.
/// <br />
/// en: The actor state type. Preferably an immutable record.
/// </typeparam>
public abstract class TActor<TState>
{
    /// <summary>
    /// hu: Az aktor kezdőállapota, amit a TActorSystem a spawn pillanatában hív meg.
    /// <br />
    /// en: The actor's initial state, invoked by TActorSystem at spawn time.
    /// </summary>
    /// <returns>
    /// hu: A kezdő TState érték.
    /// <br />
    /// en: The initial TState value.
    /// </returns>
    public abstract TState Init();

    /// <summary>
    /// hu: Egy üzenet feldolgozása. A jelenlegi állapotot, az üzenetet és a futtatási
    /// kontextust kapja, visszaadja az új állapotot. A handler az aktor állapota szempontjából
    /// pure — az egyetlen megengedett mellékhatás más aktoroknak küldött üzenet a context.Send-en
    /// keresztül.
    /// <br />
    /// en: Handle a single message. Receives the current state, the message, and the execution
    /// context, returns the new state. The handler is pure w.r.t. actor state — the only
    /// legitimate side-effect is sending messages to other actors via context.Send.
    /// </summary>
    /// <param name="AState">
    /// hu: A jelenlegi aktor állapot.
    /// <br />
    /// en: The current actor state.
    /// </param>
    /// <param name="AMessage">
    /// hu: A feldolgozandó üzenet. Soha nem null.
    /// <br />
    /// en: The message to handle. Never null.
    /// </param>
    /// <param name="AContext">
    /// hu: A futtatási kontextus: Self referencia és üzenetküldési képesség.
    /// <br />
    /// en: The execution context: Self reference and message-sending capability.
    /// </param>
    /// <returns>
    /// hu: Az új aktor állapot üzenet után.
    /// <br />
    /// en: The new actor state after the message.
    /// </returns>
    public abstract TState Handle(TState AState, object AMessage, IActorContext AContext);
}
