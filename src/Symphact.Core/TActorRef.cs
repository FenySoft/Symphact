namespace Symphact.Core;

/// <summary>
/// hu: Egy aktorra mutató nem hamisítható referencia — capability token. Jelenleg az ActorId
/// egy 64-bites egész, a jövőbeli bővítés (core-id + mailbox offset + HMAC tag + permissions)
/// ugyanezt az API felszínt fogja megőrizni. Value type: equality és hash az ActorId alapján,
/// a default érték érvénytelen (IsValid=false).
/// <br />
/// en: A non-forgeable reference to an actor — a capability token. Currently the ActorId is a
/// 64-bit integer; the future extension (core-id + mailbox offset + HMAC tag + permissions)
/// will preserve the same API surface. Value type: equality and hash based on ActorId,
/// default value is invalid (IsValid=false).
/// </summary>
public readonly record struct TActorRef(long ActorId)
{
    /// <summary>
    /// hu: Az érvénytelen (null-jellegű) aktor referencia. Azonos a default(TActorRef) értékkel.
    /// Egy ilyen referenciára küldött üzenet eldobódik vagy trap-et generál.
    /// <br />
    /// en: The invalid (null-like) actor reference. Equal to default(TActorRef). A message sent
    /// to such a reference is dropped or raises a trap.
    /// </summary>
    public static TActorRef Invalid => default;

    /// <summary>
    /// hu: true, ha ez egy létező aktorra mutató érvényes referencia (ActorId &gt; 0).
    /// <br />
    /// en: true if this is a valid reference to an existing actor (ActorId &gt; 0).
    /// </summary>
    public bool IsValid => ActorId > 0;

    /// <summary>
    /// hu: Emberi olvasható reprezentáció debug/log célokra.
    /// <br />
    /// en: Human-readable representation for debug/log purposes.
    /// </summary>
    public override string ToString() => IsValid ? $"actor#{ActorId}" : "actor#invalid";
}
