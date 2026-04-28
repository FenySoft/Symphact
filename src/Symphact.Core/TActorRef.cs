namespace Symphact.Core;

/// <summary>
/// hu: Egy aktorra mutató nem hamisítható referencia — capability token. A SlotIndex egy opaque
/// 32-bites CST (Capability Slot Table) index, amelyet a hardver old fel futásidőben
/// (core-id + mailbox offset + jogosultságok). Value type: equality és hash a SlotIndex alapján,
/// a default érték érvénytelen (IsValid=false).
/// <br />
/// en: A non-forgeable reference to an actor — a capability token. SlotIndex is an opaque
/// 32-bit CST (Capability Slot Table) index resolved by hardware at runtime
/// (core-id + mailbox offset + permissions). Value type: equality and hash based on SlotIndex,
/// default value is invalid (IsValid=false).
/// </summary>
public readonly record struct TActorRef(int SlotIndex)
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
    /// hu: true, ha ez egy létező aktorra mutató érvényes referencia (SlotIndex &gt; 0).
    /// <br />
    /// en: true if this is a valid reference to an existing actor (SlotIndex &gt; 0).
    /// </summary>
    public bool IsValid => SlotIndex > 0;

    /// <summary>
    /// hu: Emberi olvasható reprezentáció debug/log célokra.
    /// <br />
    /// en: Human-readable representation for debug/log purposes.
    /// </summary>
    public override string ToString() => IsValid ? $"actor#{SlotIndex}" : "actor#invalid";
}
