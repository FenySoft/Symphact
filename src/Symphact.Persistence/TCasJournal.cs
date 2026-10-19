using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Symphact.Persistence;

/// <summary>
/// hu: Tartalom-címzett (SHA-256) fájlrendszeres journal. A payload bájtjai az
/// <c>objects/&lt;sha256&gt;</c> fájlban élnek (azonos tartalom egyszer tárolódik), a stream
/// bejegyzései a <c>streams/&lt;sha256(persistenceId)&gt;.log</c> index-fájlban, hash-lánccal:
/// minden bejegyzés lánc-hash-e tartalmazza az előzőét, így a módosítás, a sorrend-csere és
/// a blob-csere olvasáskor <see cref="InvalidDataException"/>-t ad. Írás: ideiglenes fájl,
/// atomi átnevezés, fsync. BCL-only.
/// <br />
/// en: Content-addressed (SHA-256) file-system journal. Payload bytes live in
/// <c>objects/&lt;sha256&gt;</c> (identical content is stored once); stream entries live in
/// <c>streams/&lt;sha256(persistenceId)&gt;.log</c> with a hash chain: each entry's chain hash
/// covers the previous one, so modification, reordering and blob replacement raise
/// <see cref="InvalidDataException"/> on read. Writes: temp file, atomic rename, fsync.
/// BCL-only.
/// </summary>
public sealed class TCasJournal : IJournal
{
    private readonly TCasFiles FFiles;
    private readonly SemaphoreSlim FLock = new(1, 1);

    /// <summary>
    /// hu: Létrehozás a gyökérkönyvtárral (ha nem létezik, létrejön).
    /// <br />
    /// en: Create with the root directory (created if missing).
    /// </summary>
    /// <param name="ARootDirectory">
    /// hu: A tár gyökérkönyvtára.
    /// <br />
    /// en: The store root directory.
    /// </param>
    /// <param name="ASerializer">
    /// hu: Payload szerializáló; alapértelmezés: <see cref="TJsonPayloadSerializer"/>.
    /// <br />
    /// en: Payload serializer; default: <see cref="TJsonPayloadSerializer"/>.
    /// </param>
    public TCasJournal(string ARootDirectory, IPayloadSerializer? ASerializer = null)
    {
        FFiles = new TCasFiles(ARootDirectory, TCasFiles.StreamsDirectoryName, ASerializer);
    }

    /// <inheritdoc />
    public async Task<long> AppendAsync(
        string APersistenceId,
        object APayload,
        CancellationToken ACancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(APersistenceId);
        ArgumentNullException.ThrowIfNull(APayload);

        var bytes = FFiles.Serialize(APayload);
        var payloadHash = TCasFiles.Hex(SHA256.HashData(bytes));

        await FLock.WaitAsync(ACancellationToken).ConfigureAwait(false);

        try
        {
            using var root = await FFiles.EnterRootAsync(ACancellationToken).ConfigureAwait(false);

            var state = ReadState(APersistenceId);
            var seq = state.Highest + 1;
            var chain = TCasFiles.Chain(state.Chain, seq, payloadHash);

            FFiles.WriteBlob(payloadHash, bytes);
            FFiles.AppendLine(APersistenceId, string.Join(' ', "E",
                seq.ToString(CultureInfo.InvariantCulture),
                DateTimeOffset.UtcNow.UtcTicks.ToString(CultureInfo.InvariantCulture),
                payloadHash, chain));

            return seq;
        }
        finally
        {
            FLock.Release();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TJournalEntry> ReadAsync(
        string APersistenceId,
        long AFromSequenceNr = 0,
        [EnumeratorCancellation] CancellationToken ACancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(APersistenceId);

        List<TJournalEntry> entries;

        await FLock.WaitAsync(ACancellationToken).ConfigureAwait(false);

        try
        {
            entries = ReadState(APersistenceId).Entries
                .Where(e => e.SequenceNr >= AFromSequenceNr)
                .Select(e => new TJournalEntry(APersistenceId, e.SequenceNr, e.Payload, e.Timestamp))
                .ToList();
        }
        finally
        {
            FLock.Release();
        }

        foreach (var entry in entries)
        {
            ACancellationToken.ThrowIfCancellationRequested();

            yield return entry;
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        string APersistenceId,
        long AToSequenceNr,
        CancellationToken ACancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(APersistenceId);

        await FLock.WaitAsync(ACancellationToken).ConfigureAwait(false);

        try
        {
            using var root = await FFiles.EnterRootAsync(ACancellationToken).ConfigureAwait(false);

            if (ReadState(APersistenceId).Highest == 0)
            {
                return;
            }

            FFiles.AppendLine(APersistenceId, "D " + AToSequenceNr.ToString(CultureInfo.InvariantCulture));
        }
        finally
        {
            FLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<long> GetHighestSequenceNrAsync(
        string APersistenceId,
        CancellationToken ACancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(APersistenceId);

        await FLock.WaitAsync(ACancellationToken).ConfigureAwait(false);

        try
        {
            return ReadState(APersistenceId).Highest;
        }
        finally
        {
            FLock.Release();
        }
    }

    private sealed record TIndexed(long SequenceNr, string PayloadHash, DateTimeOffset Timestamp);

    private sealed record TLoaded(long SequenceNr, object Payload, DateTimeOffset Timestamp);

    private sealed record TState(long Highest, string Chain, List<TLoaded> Entries);

    private TState ReadState(string APersistenceId)
    {
        var path = FFiles.IndexPath(APersistenceId);
        var entries = new List<TLoaded>();
        var indexed = new List<TIndexed>();
        long highest = 0;
        long deletedTo = 0;
        var chain = TCasFiles.Genesis;

        if (!File.Exists(path))
        {
            return new TState(0, chain, entries);
        }

        foreach (var line in File.ReadAllLines(path))
        {
            var parts = line.Split(' ');

            if (parts[0] == "D" && parts.Length == 2)
            {
                deletedTo = Math.Max(deletedTo, long.Parse(parts[1], CultureInfo.InvariantCulture));

                continue;
            }

            if (parts[0] != "E" || parts.Length != 5)
            {
                throw new InvalidDataException("Malformed journal index line.");
            }

            var seq = long.Parse(parts[1], CultureInfo.InvariantCulture);
            var expected = TCasFiles.Chain(chain, seq, parts[3]);

            if (seq != highest + 1 || expected != parts[4])
            {
                throw new InvalidDataException("Journal hash chain broken at sequence " + seq + ".");
            }

            chain = expected;
            highest = seq;
            indexed.Add(new TIndexed(
                seq,
                parts[3],
                new DateTimeOffset(long.Parse(parts[2], CultureInfo.InvariantCulture), TimeSpan.Zero)));
        }

        foreach (var item in indexed.Where(e => e.SequenceNr > deletedTo))
        {
            entries.Add(new TLoaded(item.SequenceNr, FFiles.LoadBlob(item.PayloadHash), item.Timestamp));
        }

        return new TState(highest, chain, entries);
    }
}
