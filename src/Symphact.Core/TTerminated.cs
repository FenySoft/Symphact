namespace Symphact.Core;

/// <summary>
/// hu: DeathWatch rendszerüzenet. Egy figyelő aktor mailboxába kerül, ha a figyelt aktor
/// véglegesen leállt (Stop). Restart NEM küld TTerminated-et — az aktor ilyenkor "él" még.
/// <br />
/// en: DeathWatch system message. Delivered to a watcher actor's mailbox when the watched
/// actor permanently stops. Restart does NOT send TTerminated — the actor is still "alive".
/// </summary>
/// <param name="Actor">
/// hu: A leállt aktor referenciája.
/// <br />
/// en: The reference of the stopped actor.
/// </param>
public sealed record TTerminated(TActorRef Actor);
