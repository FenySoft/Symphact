namespace Symphact.Persistence;

/// <summary>
/// hu: A snapshot store absztrakció — egy aktor (vagy bármely más entitás) állapot-
/// pillanatképeinek tartós tárolója. A snapshot egy adott SequenceNr-ig fel-összegzett
/// állapot, amelyet a recovery a teljes journal-replay helyett betölthet, majd csak a
/// SequenceNr utáni eseményeket játssza vissza. Stream-orientált: minden stream-et egy
/// PersistenceId azonosít — szemantikailag ugyanaz a stream-fogalom, mint az
/// <see cref="IJournal"/>-ban.
/// <br />
/// Az interface BCL-only — a Symphact.Persistence csomag nem hoz be NuGet függőséget; a
/// konkrét implementációk (in-memory, SQLite, hardveres DMA) külön platform csomagokban
/// élnek. A SequenceNr-ek értelmezését a hívó dönti el (tipikusan a journal által kiosztott
/// sorszám), a snapshot store pusztán kulcs-érték tárként viselkedik kompakció-támogatással.
/// <br />
/// CFPU jegyzet: a hardveres snapshot store várhatóan ugyanazon DMA engine-re épül, amelyet
/// a journal is használ — a core SRAM-állapot egészben kerül kiírásra külső DRAM/flash-re,
/// így restart után egyetlen DMA-olvasással helyreállítható.
/// <br />
/// en: The snapshot store abstraction — durable storage of state snapshots for an actor
/// (or any other entity). A snapshot is the state aggregated up to a given SequenceNr,
/// which recovery can load instead of replaying the entire journal, then replay only the
/// events after that SequenceNr. Stream-oriented: each stream is identified by a
/// PersistenceId — the same stream concept as in <see cref="IJournal"/>.
/// <br />
/// The interface is BCL-only — the Symphact.Persistence package introduces no NuGet
/// dependencies; concrete implementations (in-memory, SQLite, hardware DMA) live in
/// separate platform packages. SequenceNr semantics are decided by the caller (typically
/// the journal-assigned sequence number); the snapshot store behaves as a key-value store
/// with compaction support.
/// <br />
/// CFPU note: the hardware snapshot store is expected to share the DMA engine with the
/// journal — core SRAM state is written out as a whole to external DRAM/flash, so after a
/// restart it can be restored with a single DMA read.
/// </summary>
public interface ISnapshotStore
{
    /// <summary>
    /// hu: Snapshot mentése. Több snapshot is létezhet ugyanazon stream-en (történelem),
    /// különböző SequenceNr-rel. Ha azonos PersistenceId + SequenceNr párral már létezik
    /// snapshot, az új payload felülírja — a duplikáció nem hiba (a hívó újrapróbálkozhat
    /// például restart után). A művelet atomikus.
    /// <br />
    /// en: Save a snapshot. Multiple snapshots may exist for the same stream (history) with
    /// different SequenceNr values. If a snapshot already exists with the same
    /// PersistenceId + SequenceNr pair, the new payload overwrites it — duplicates are not
    /// an error (the caller may retry e.g. after a restart). The operation is atomic.
    /// </summary>
    /// <param name="APersistenceId">
    /// hu: A stream azonosítója. Nem null, nem üres.
    /// <br />
    /// en: The stream identifier. Not null, not empty.
    /// </param>
    /// <param name="ASequenceNr">
    /// hu: A snapshot által összegzett utolsó esemény sorszáma. Pozitív (≥ 1).
    /// <br />
    /// en: The sequence number of the last event aggregated by the snapshot. Positive (≥ 1).
    /// </param>
    /// <param name="APayload">
    /// hu: A snapshot tartalma. Nem null. A store nem értelmezi — opaque.
    /// <br />
    /// en: The snapshot payload. Not null. Opaque to the store.
    /// </param>
    /// <param name="ACancellationToken">
    /// hu: Megszakítási token.
    /// <br />
    /// en: Cancellation token.
    /// </param>
    Task SaveAsync(
        string APersistenceId,
        long ASequenceNr,
        object APayload,
        CancellationToken ACancellationToken = default);

    /// <summary>
    /// hu: A legfrissebb snapshot betöltése egy stream-ről, opcionális felső sorszám-
    /// határral. Recovery során tipikusan határ nélkül hívják — a legfrissebb pillanatkép
    /// kell. Ha a snapshot store-ban a recovery-célnál újabb pillanatképek is vannak (pl.
    /// időutazás, partial replay), a hívó megadhat egy AMaxSequenceNr-t, és a store a
    /// legmagasabb olyan snapshotot adja vissza, amelynek SequenceNr ≤ AMaxSequenceNr.
    /// <br />
    /// en: Load the most recent snapshot of a stream, with an optional upper sequence-number
    /// bound. During recovery this is typically called without a bound — the freshest
    /// snapshot is wanted. If the store contains snapshots newer than the recovery target
    /// (e.g. time-travel, partial replay), the caller can supply AMaxSequenceNr, and the
    /// store returns the snapshot with the highest SequenceNr ≤ AMaxSequenceNr.
    /// </summary>
    /// <param name="APersistenceId">
    /// hu: A stream azonosítója. Nem null, nem üres.
    /// <br />
    /// en: The stream identifier. Not null, not empty.
    /// </param>
    /// <param name="AMaxSequenceNr">
    /// hu: A figyelembe vett legmagasabb sorszám (inclusive). Default: long.MaxValue
    /// (= "add a legfrissebbet").
    /// <br />
    /// en: The highest sequence number considered (inclusive). Default: long.MaxValue
    /// (= "give me the freshest one").
    /// </param>
    /// <param name="ACancellationToken">
    /// hu: Megszakítási token.
    /// <br />
    /// en: Cancellation token.
    /// </param>
    /// <returns>
    /// hu: A kiválasztott snapshot, vagy null ha nincs ilyen (ismeretlen stream, üres stream,
    /// vagy minden snapshot SequenceNr-je &gt; AMaxSequenceNr).
    /// <br />
    /// en: The selected snapshot, or null if none matches (unknown stream, empty stream, or
    /// all snapshots have SequenceNr &gt; AMaxSequenceNr).
    /// </returns>
    Task<TSnapshotEntry?> LoadAsync(
        string APersistenceId,
        long AMaxSequenceNr = long.MaxValue,
        CancellationToken ACancellationToken = default);

    /// <summary>
    /// hu: Egy stream snapshotjainak törlése a megadott sorszámmal bezárólag (inclusive).
    /// Tipikus használat: friss snapshot mentése után a régi pillanatképek kompaktálása.
    /// A HighestSequenceNr NEM csökken — a logikai pozíció nem vész el (a journal
    /// szemantikájával összhangban).
    /// <br />
    /// en: Delete snapshots of a stream up to the given sequence number (inclusive).
    /// Typical use: compaction of older snapshots after saving a fresh one.
    /// HighestSequenceNr does NOT decrease — the logical position is not lost (consistent
    /// with the journal semantics).
    /// </summary>
    /// <param name="APersistenceId">
    /// hu: A stream azonosítója. Ismeretlen stream esetén no-op.
    /// <br />
    /// en: The stream identifier. No-op for unknown streams.
    /// </param>
    /// <param name="AToSequenceNr">
    /// hu: A bezárólag törlendő sorszám.
    /// <br />
    /// en: The (inclusive) sequence number up to which snapshots are deleted.
    /// </param>
    /// <param name="ACancellationToken">
    /// hu: Megszakítási token.
    /// <br />
    /// en: Cancellation token.
    /// </param>
    Task DeleteAsync(
        string APersistenceId,
        long AToSequenceNr,
        CancellationToken ACancellationToken = default);

    /// <summary>
    /// hu: Egy stream eddig elmentett snapshotjai közül a legmagasabb SequenceNr lekérdezése.
    /// 0, ha a stream nem létezik vagy soha nem mentettek bele snapshotot. A Delete nem
    /// csökkenti — a logikai pozíció nem vész el (a journal szemantikájával összhangban).
    /// <br />
    /// en: Returns the highest SequenceNr among the snapshots ever saved for a stream.
    /// 0 if the stream does not exist or no snapshot has ever been saved. Delete does not
    /// decrease this — the logical position is not lost (consistent with the journal
    /// semantics).
    /// </summary>
    /// <param name="APersistenceId">
    /// hu: A stream azonosítója.
    /// <br />
    /// en: The stream identifier.
    /// </param>
    /// <param name="ACancellationToken">
    /// hu: Megszakítási token.
    /// <br />
    /// en: Cancellation token.
    /// </param>
    /// <returns>
    /// hu: A legmagasabb mentett SequenceNr, vagy 0 ha üres / ismeretlen.
    /// <br />
    /// en: The highest saved SequenceNr, or 0 if empty / unknown.
    /// </returns>
    Task<long> GetHighestSequenceNrAsync(
        string APersistenceId,
        CancellationToken ACancellationToken = default);
}
