namespace Symphact.Persistence;

/// <summary>
/// hu: Egy snapshot bejegyzés — egy aktor (vagy bármely más entitás) állapotának egy adott
/// pillanatban felvett képe a hozzá tartozó stream sorszámával és UTC időbélyeggel. A
/// snapshot annyit jelent, hogy a stream az adott SequenceNr-ig "fel van összegezve" ebbe
/// az állapotba — recovery során a journal csak a SequenceNr utáni eseményeket kell
/// visszajátszania. Érték típus (record struct), így a snapshot store felülete sem generál
/// GC nyomást.
/// <br />
/// FONTOS: a SequenceNr egy adott PersistenceId stream-en belül egyezik a journal által
/// kiosztott sorszámmal — ez az invariáns kapcsolja össze a snapshot store-t és a journal-t.
/// A snapshot store nem ellenőrzi az invariánst (a hívó felelőssége), csak monoton
/// kompakciót tesz lehetővé.
/// <br />
/// en: A snapshot entry — the captured state of an actor (or any other entity) at a
/// specific point in its stream, with the matching sequence number and UTC timestamp. A
/// snapshot means that the stream is "summed up" into this state up to the given
/// SequenceNr — during recovery the journal only needs to replay events after this
/// SequenceNr. Value type (record struct) so the snapshot store surface adds no GC pressure.
/// <br />
/// IMPORTANT: SequenceNr within a given PersistenceId stream matches the sequence number
/// assigned by the journal — this invariant ties the snapshot store and the journal
/// together. The snapshot store does not enforce this invariant (the caller is responsible);
/// it only enables monotonic compaction.
/// </summary>
/// <param name="PersistenceId">
/// hu: A stream azonosítója, amelyhez a snapshot tartozik.
/// <br />
/// en: The identifier of the stream this snapshot belongs to.
/// </param>
/// <param name="SequenceNr">
/// hu: A snapshot sorszáma a stream-en belül — az utolsó esemény sorszáma, amelyet a
/// snapshot magába összegez. Pozitív (≥ 1).
/// <br />
/// en: The sequence number of the snapshot within the stream — the sequence number of the
/// last event that the snapshot aggregates. Positive (≥ 1).
/// </param>
/// <param name="Payload">
/// hu: A snapshot tartalma — opaque object, a snapshot store nem értelmezi. A
/// szerializációt a konkrét implementáció végzi (in-memory esetén nincs).
/// <br />
/// en: The snapshot payload — an opaque object that the snapshot store does not interpret.
/// The concrete implementation handles serialisation (in-memory does none).
/// </param>
/// <param name="Timestamp">
/// hu: A snapshot UTC időbélyege a mentés pillanatában.
/// <br />
/// en: The UTC timestamp captured at the moment of save.
/// </param>
public readonly record struct TSnapshotEntry(
    string PersistenceId,
    long SequenceNr,
    object Payload,
    DateTimeOffset Timestamp);
