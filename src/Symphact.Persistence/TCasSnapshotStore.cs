using System.Globalization;
using System.Security.Cryptography;

namespace Symphact.Persistence;

/// <summary>
/// hu: Tartalom-címzett (SHA-256) fájlrendszeres snapshot tár. A payload bájtjai az
/// <c>objects/&lt;sha256&gt;</c> fájlban élnek (azonos tartalom egyszer tárolódik, a journallal
/// közös objektumtárban is), a stream snapshotjai a <c>snapshots/&lt;sha256(persistenceId)&gt;.log</c>
/// index-fájlban, hash-lánccal: a módosítás vagy a blob-csere olvasáskor
/// <see cref="InvalidDataException"/>-t ad. BCL-only.
/// <br />
/// en: Content-addressed (SHA-256) file-system snapshot store. Payload bytes live in
/// <c>objects/&lt;sha256&gt;</c> (identical content is stored once, also across the journal's
/// shared object store); a stream's snapshots live in the
/// <c>snapshots/&lt;sha256(persistenceId)&gt;.log</c> index file with a hash chain: modification
/// or blob replacement raises <see cref="InvalidDataException"/> on read. BCL-only.
/// </summary>
public sealed class TCasSnapshotStore : ISnapshotStore
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
    public TCasSnapshotStore(string ARootDirectory, IPayloadSerializer? ASerializer = null)
    {
        FFiles = new TCasFiles(ARootDirectory, TCasFiles.SnapshotsDirectoryName, ASerializer);
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        string APersistenceId,
        long ASequenceNr,
        object APayload,
        CancellationToken ACancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(APersistenceId);
        ArgumentOutOfRangeException.ThrowIfLessThan(ASequenceNr, 1);
        ArgumentNullException.ThrowIfNull(APayload);

        var bytes = FFiles.Serialize(APayload);
        var payloadHash = TCasFiles.Hex(SHA256.HashData(bytes));

        await FLock.WaitAsync(ACancellationToken).ConfigureAwait(false);

        try
        {
            using var root = await FFiles.EnterRootAsync(ACancellationToken).ConfigureAwait(false);

            var chain = TCasFiles.Chain(ReadState(APersistenceId).Chain, ASequenceNr, payloadHash);

            FFiles.WriteBlob(payloadHash, bytes);
            FFiles.AppendLine(APersistenceId, string.Join(' ', "S",
                ASequenceNr.ToString(CultureInfo.InvariantCulture),
                DateTimeOffset.UtcNow.UtcTicks.ToString(CultureInfo.InvariantCulture),
                payloadHash, chain));
        }
        finally
        {
            FLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<TSnapshotEntry?> LoadAsync(
        string APersistenceId,
        long AMaxSequenceNr = long.MaxValue,
        CancellationToken ACancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(APersistenceId);

        await FLock.WaitAsync(ACancellationToken).ConfigureAwait(false);

        try
        {
            var state = ReadState(APersistenceId);
            var best = state.Live.Where(e => e.Key <= AMaxSequenceNr).OrderByDescending(e => e.Key)
                .Select(e => e.Value).FirstOrDefault();

            return best is null
                ? null
                : new TSnapshotEntry(APersistenceId, best.SequenceNr, FFiles.LoadBlob(best.PayloadHash), best.Timestamp);
        }
        finally
        {
            FLock.Release();
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

    private sealed record TLive(long SequenceNr, string PayloadHash, DateTimeOffset Timestamp);

    private sealed record TState(long Highest, string Chain, Dictionary<long, TLive> Live);

    private TState ReadState(string APersistenceId)
    {
        var path = FFiles.IndexPath(APersistenceId);
        var live = new Dictionary<long, TLive>();
        long highest = 0;
        var chain = TCasFiles.Genesis;

        if (!File.Exists(path))
        {
            return new TState(0, chain, live);
        }

        foreach (var line in File.ReadAllLines(path))
        {
            var parts = line.Split(' ');

            if (parts[0] == "D" && parts.Length == 2)
            {
                var to = long.Parse(parts[1], CultureInfo.InvariantCulture);

                foreach (var key in live.Keys.Where(k => k <= to).ToList())
                {
                    live.Remove(key);
                }

                continue;
            }

            if (parts[0] != "S" || parts.Length != 5)
            {
                throw new InvalidDataException("Malformed snapshot index line.");
            }

            var seq = long.Parse(parts[1], CultureInfo.InvariantCulture);
            var expected = TCasFiles.Chain(chain, seq, parts[3]);

            if (expected != parts[4])
            {
                throw new InvalidDataException("Snapshot hash chain broken at sequence " + seq + ".");
            }

            chain = expected;
            highest = Math.Max(highest, seq);
            live[seq] = new TLive(
                seq,
                parts[3],
                new DateTimeOffset(long.Parse(parts[2], CultureInfo.InvariantCulture), TimeSpan.Zero));
        }

        return new TState(highest, chain, live);
    }
}
