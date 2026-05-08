namespace Symphact.Persistence;

/// <summary>
/// hu: Memóriában tartott snapshot store — a perzisztencia HAL teszt és diagnosztikai célú
/// referencia implementációja az <see cref="ISnapshotStore"/>-hoz. Thread-safe egyetlen
/// lock-kal (egyszerűség elsőbbsége), a snapshotok stream-enként SequenceNr szerint
/// rendezett listában tárolódnak. A payload-ok eredeti object referenciaként kerülnek el —
/// nincs szerializáció.
/// <br />
/// en: In-memory snapshot store — reference implementation of the persistence HAL for
/// <see cref="ISnapshotStore"/>, intended for testing and diagnostics. Thread-safe with a
/// single lock (simplicity first); snapshots are kept in per-stream lists ordered by
/// SequenceNr. Payloads are stored as the original object reference — no serialisation.
/// </summary>
public sealed class TInMemorySnapshotStore : ISnapshotStore
{
    private readonly object FLock = new();
    private readonly Dictionary<string, TStream> FStreams = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task SaveAsync(
        string APersistenceId,
        long ASequenceNr,
        object APayload,
        CancellationToken ACancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(APersistenceId);
        ArgumentNullException.ThrowIfNull(APayload);

        if (APersistenceId.Length == 0)
            throw new ArgumentException("Persistence id must not be empty.", nameof(APersistenceId));

        if (ASequenceNr <= 0)
            throw new ArgumentOutOfRangeException(nameof(ASequenceNr), ASequenceNr, "Sequence number must be positive (≥ 1).");

        ACancellationToken.ThrowIfCancellationRequested();

        var entry = new TSnapshotEntry(APersistenceId, ASequenceNr, APayload, DateTimeOffset.UtcNow);

        lock (FLock)
        {
            if (!FStreams.TryGetValue(APersistenceId, out var stream))
            {
                stream = new TStream();
                FStreams[APersistenceId] = stream;
            }

            // Snapshots are kept ordered by SequenceNr ascending. If a snapshot with the
            // same SequenceNr already exists, overwrite it; otherwise insert at the right
            // position. Linear scan is fine for the in-memory reference impl.
            var insertIndex = 0;

            while (insertIndex < stream.Snapshots.Count
                   && stream.Snapshots[insertIndex].SequenceNr < ASequenceNr)
            {
                insertIndex++;
            }

            if (insertIndex < stream.Snapshots.Count
                && stream.Snapshots[insertIndex].SequenceNr == ASequenceNr)
            {
                stream.Snapshots[insertIndex] = entry;
            }
            else
            {
                stream.Snapshots.Insert(insertIndex, entry);
            }

            if (ASequenceNr > stream.HighestSequenceNr)
                stream.HighestSequenceNr = ASequenceNr;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<TSnapshotEntry?> LoadAsync(
        string APersistenceId,
        long AMaxSequenceNr = long.MaxValue,
        CancellationToken ACancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(APersistenceId);

        if (APersistenceId.Length == 0)
            throw new ArgumentException("Persistence id must not be empty.", nameof(APersistenceId));

        ACancellationToken.ThrowIfCancellationRequested();

        lock (FLock)
        {
            if (!FStreams.TryGetValue(APersistenceId, out var stream) || stream.Snapshots.Count == 0)
                return Task.FromResult<TSnapshotEntry?>(null);

            // Snapshots are ordered by SequenceNr ascending — walk from the end to find the
            // highest entry with SequenceNr ≤ AMaxSequenceNr.
            for (var i = stream.Snapshots.Count - 1; i >= 0; i--)
            {
                if (stream.Snapshots[i].SequenceNr <= AMaxSequenceNr)
                    return Task.FromResult<TSnapshotEntry?>(stream.Snapshots[i]);
            }

            return Task.FromResult<TSnapshotEntry?>(null);
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(
        string APersistenceId,
        long AToSequenceNr,
        CancellationToken ACancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(APersistenceId);
        ACancellationToken.ThrowIfCancellationRequested();

        lock (FLock)
        {
            if (!FStreams.TryGetValue(APersistenceId, out var stream))
                return Task.CompletedTask;

            var removeCount = 0;

            while (removeCount < stream.Snapshots.Count
                   && stream.Snapshots[removeCount].SequenceNr <= AToSequenceNr)
            {
                removeCount++;
            }

            if (removeCount > 0)
                stream.Snapshots.RemoveRange(0, removeCount);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<long> GetHighestSequenceNrAsync(
        string APersistenceId,
        CancellationToken ACancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(APersistenceId);
        ACancellationToken.ThrowIfCancellationRequested();

        lock (FLock)
        {
            if (!FStreams.TryGetValue(APersistenceId, out var stream))
                return Task.FromResult(0L);

            return Task.FromResult(stream.HighestSequenceNr);
        }
    }

    private sealed class TStream
    {
        public List<TSnapshotEntry> Snapshots { get; } = new();
        public long HighestSequenceNr { get; set; }
    }
}
