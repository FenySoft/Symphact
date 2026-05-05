namespace Symphact.Persistence;

/// <summary>
/// hu: Egy journal bejegyzés — egy stream egy aposztolt eseménye / üzenete a hozzá tartozó
/// monoton sorszámmal és időbélyeggel. Érték típus (record struct), így a journal-heavy
/// terhelésen sem generál GC nyomást a payload borítékaként.
/// <br />
/// FONTOS: a SequenceNr egy adott PersistenceId stream-en belül szigorúan monoton növekvő,
/// 1-től indul. Különböző stream-ek között nem összevethető.
/// <br />
/// en: A journal entry — a single appended event / message of a stream with its monotonic
/// sequence number and timestamp. Value type (record struct) so the entry envelope adds
/// no GC pressure on journal-heavy workloads.
/// <br />
/// IMPORTANT: SequenceNr is strictly monotonic within a given PersistenceId stream and
/// starts at 1. Sequence numbers from different streams are not comparable.
/// </summary>
/// <param name="PersistenceId">
/// hu: A stream azonosítója, amelyhez a bejegyzés tartozik.
/// <br />
/// en: The identifier of the stream this entry belongs to.
/// </param>
/// <param name="SequenceNr">
/// hu: A bejegyzés monoton sorszáma a stream-en belül (1-től).
/// <br />
/// en: The monotonic sequence number of the entry within the stream (starting at 1).
/// </param>
/// <param name="Payload">
/// hu: A bejegyzés tartalma — opaque object, a journal nem értelmezi. A szerializációt a
/// konkrét journal implementáció végzi (in-memory esetén nincs).
/// <br />
/// en: The entry payload — an opaque object that the journal does not interpret. The
/// concrete journal implementation handles serialisation (in-memory does none).
/// </param>
/// <param name="Timestamp">
/// hu: A bejegyzés UTC időbélyege a hozzáadás pillanatában.
/// <br />
/// en: The UTC timestamp captured at the moment of append.
/// </param>
public readonly record struct TJournalEntry(
    string PersistenceId,
    long SequenceNr,
    object Payload,
    DateTimeOffset Timestamp);
