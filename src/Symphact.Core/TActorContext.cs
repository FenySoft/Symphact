namespace Symphact.Core;

/// <summary>
/// hu: Az IActorContext referencia-implementációja. Egyetlen Handle hívásra jön létre:
/// tartalmazza a futó aktor Self referenciáját és a TActorSystem-hez való delegálást.
/// Értéktípus (readonly struct) — stack-en él, de IActorContext-ként való átadáskor
/// boxing történik (heap allokáció). A CFPU hardveres context regiszter szoftveres
/// megfelelője, ahol a kontextus regiszterbe kerül, nem a heap-re.
/// <br />
/// en: Reference implementation of IActorContext. Created per Handle invocation: holds
/// the running actor's Self reference and delegation to TActorSystem. Value type
/// (readonly struct) — lives on the stack, but boxing occurs when passed as IActorContext
/// (heap allocation). Software equivalent of the CFPU hardware context register, where
/// the context lives in a register rather than on the heap.
/// </summary>
public readonly struct TActorContext : IActorContext
{
    private readonly TActorSystem FSystem;

    /// <summary>
    /// hu: Új kontextus létrehozása a megadott rendszerhez és aktor-referenciához.
    /// <br />
    /// en: Creates a new context for the given system and actor reference.
    /// </summary>
    /// <param name="ASystem">
    /// hu: A futó aktor rendszere — üzenetküldéshez szükséges.
    /// <br />
    /// en: The actor system of the running actor — required for message sending.
    /// </param>
    /// <param name="ASelf">
    /// hu: A jelenleg futó aktor referenciája.
    /// <br />
    /// en: The reference of the currently executing actor.
    /// </param>
    internal TActorContext(TActorSystem ASystem, TActorRef ASelf)
    {
        FSystem = ASystem;
        Self = ASelf;
    }

    /// <inheritdoc />
    public TActorRef Self { get; }

    /// <inheritdoc />
    public void Send(TActorRef ATarget, object AMessage) => FSystem.Send(ATarget, AMessage);

    /// <inheritdoc />
    public TActorRef Spawn<TActorType, TState>()
        where TActorType : TActor<TState>, new()
        => FSystem.SpawnChild<TActorType, TState>(Self);

    /// <inheritdoc />
    public void Watch(TActorRef ATarget) => FSystem.WatchActor(Self, ATarget);

    /// <inheritdoc />
    public void Unwatch(TActorRef ATarget) => FSystem.UnwatchActor(Self, ATarget);
}
