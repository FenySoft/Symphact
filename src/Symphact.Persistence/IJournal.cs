namespace Symphact.Persistence;

/// <summary>
/// hu: A journal absztrakció — egy aktor (vagy bármely más entitás) eseményeinek tartós
/// tárolója. Stream-orientált: minden stream-et egy persistenceId azonosít, a bejegyzések
/// monoton sorszámot kapnak (1-től). Az interface BCL-only — a Symphact.Persistence csomag
/// nem hoz be NuGet függőséget; a konkrét implementációk (in-memory, SQLite, hardveres
/// DMA) külön platform csomagokban élnek, így a CFPU drop-in útvonal tiszta marad.
/// <br />
/// CFPU jegyzet: a hardveres journal várhatóan DMA engine-re épül, amely a core SRAM-ból
/// külső DRAM/flash területre írja az eseményeket aszinkron módon. Az API szándékosan
/// async, hogy a non-blocking DMA befejezésére is illeszkedjen.
/// <br />
/// en: The journal abstraction — durable storage of an actor's (or any entity's) events.
/// Stream-oriented: each stream is identified by a persistenceId, entries receive
/// monotonic sequence numbers (starting at 1). The interface is BCL-only — the
/// Symphact.Persistence package introduces no NuGet dependencies; concrete implementations
/// (in-memory, SQLite, hardware DMA) live in separate platform packages so the CFPU
/// drop-in path remains clean.
/// <br />
/// CFPU note: the hardware journal is expected to be DMA-engine backed, writing events
/// asynchronously from core SRAM to external DRAM/flash. The API is deliberately async
/// to fit non-blocking DMA completion.
/// </summary>
public interface IJournal
{
    /// <summary>
    /// hu: Egy payload hozzáadása a stream végéhez. Atomikus művelet — több párhuzamos
    /// hívó hívásai monoton sorrendet kapnak ugyanazon stream-en belül.
    /// <br />
    /// en: Append a payload to the end of the stream. Atomic — concurrent callers receive
    /// monotonic ordering within the same stream.
    /// </summary>
    /// <param name="APersistenceId">
    /// hu: A stream azonosítója. Nem null, nem üres.
    /// <br />
    /// en: The stream identifier. Not null, not empty.
    /// </param>
    /// <param name="APayload">
    /// hu: A hozzáadandó payload. Nem null. A journal nem értelmezi — opaque.
    /// <br />
    /// en: The payload to append. Not null. Opaque to the journal.
    /// </param>
    /// <param name="ACancellationToken">
    /// hu: Megszakítási token.
    /// <br />
    /// en: Cancellation token.
    /// </param>
    /// <returns>
    /// hu: A hozzárendelt monoton sorszám (1-től).
    /// <br />
    /// en: The assigned monotonic sequence number (starting at 1).
    /// </returns>
    Task<long> AppendAsync(
        string APersistenceId,
        object APayload,
        CancellationToken ACancellationToken = default);

    /// <summary>
    /// hu: Egy stream bejegyzéseinek olvasása sorszám-rendben, a megadott sorszámtól
    /// (inclusive) kezdve. Ha a stream nem létezik, üres szekvencia. A visszaadott
    /// IAsyncEnumerable streaming módon ad vissza eredményt — nagy stream-ek esetén
    /// nem lát O(N) memóriaköltséget.
    /// <br />
    /// en: Read the entries of a stream in sequence order, starting at the given sequence
    /// number (inclusive). Empty if the stream does not exist. The returned
    /// IAsyncEnumerable streams results — no O(N) memory blow-up for large streams.
    /// </summary>
    /// <param name="APersistenceId">
    /// hu: A stream azonosítója.
    /// <br />
    /// en: The stream identifier.
    /// </param>
    /// <param name="AFromSequenceNr">
    /// hu: A kezdő sorszám (inclusive). 0 = a stream eleje.
    /// <br />
    /// en: The starting sequence number (inclusive). 0 = from the beginning.
    /// </param>
    /// <param name="ACancellationToken">
    /// hu: Megszakítási token. Az enumeráció minden lépésénél ellenőrzött.
    /// <br />
    /// en: Cancellation token. Checked at every step of enumeration.
    /// </param>
    /// <returns>
    /// hu: A stream bejegyzéseinek aszinkron szekvenciája.
    /// <br />
    /// en: The asynchronous sequence of stream entries.
    /// </returns>
    IAsyncEnumerable<TJournalEntry> ReadAsync(
        string APersistenceId,
        long AFromSequenceNr = 0,
        CancellationToken ACancellationToken = default);

    /// <summary>
    /// hu: Egy stream bejegyzéseinek törlése a megadott sorszámmal bezárólag (inclusive).
    /// Tipikus használat: snapshot készítés után a régi bejegyzések kompaktálása. A
    /// HighestSequenceNr NEM csökken — a következő Append a régi sorozat folytatásaként
    /// kap sorszámot.
    /// <br />
    /// en: Delete entries of a stream up to the given sequence number (inclusive). Typical
    /// use: compaction of older entries after a snapshot. HighestSequenceNr does NOT
    /// decrease — the next Append continues the original sequence.
    /// </summary>
    /// <param name="APersistenceId">
    /// hu: A stream azonosítója. Ismeretlen stream esetén no-op.
    /// <br />
    /// en: The stream identifier. No-op for unknown streams.
    /// </param>
    /// <param name="AToSequenceNr">
    /// hu: A bezárólag törlendő sorszám.
    /// <br />
    /// en: The (inclusive) sequence number up to which entries are deleted.
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
    /// hu: Egy stream eddig kiadott legnagyobb sorszámának lekérdezése. 0, ha a stream
    /// nem létezik vagy soha nem írtak bele. A Delete nem csökkenti — a logikai pozíció
    /// nem vész el.
    /// <br />
    /// en: Returns the highest sequence number ever assigned to a stream. 0 if the stream
    /// does not exist or was never written. Delete does not decrease this — the logical
    /// position is not lost.
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
    /// hu: A legmagasabb kiadott sorszám, vagy 0 ha üres / ismeretlen.
    /// <br />
    /// en: The highest assigned sequence number, or 0 if empty / unknown.
    /// </returns>
    Task<long> GetHighestSequenceNrAsync(
        string APersistenceId,
        CancellationToken ACancellationToken = default);
}
