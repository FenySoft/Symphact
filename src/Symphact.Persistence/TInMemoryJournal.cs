using System.Runtime.CompilerServices;

namespace Symphact.Persistence;

/// <summary>
/// hu: Memóriában tartott journal — a perzisztencia HAL teszt és diagnosztikai célú
/// referencia implementációja. Thread-safe egyetlen lock-kal (egyszerűség elsőbbsége);
/// nagy átbocsátású szerverekhez per-stream lock-ot célzó SQLite implementáció jön
/// később. A bejegyzések nem opszerializálódnak — a payload-ok eredeti object referenciaként
/// kerülnek tárolásra.
/// <br />
/// en: In-memory journal — reference implementation of the persistence HAL for testing
/// and diagnostics. Thread-safe with a single lock (simplicity first); a per-stream
/// locking SQLite implementation is planned for high-throughput servers. Entries are
/// not serialised — payloads are stored as the original object reference.
/// </summary>
public sealed class TInMemoryJournal : IJournal
{
    private readonly object FLock = new();
    private readonly Dictionary<string, TStream> FStreams = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<long> AppendAsync(
        string APersistenceId,
        object APayload,
        CancellationToken ACancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(APersistenceId);
        ArgumentNullException.ThrowIfNull(APayload);

        if (APersistenceId.Length == 0)
            throw new ArgumentException("Persistence id must not be empty.", nameof(APersistenceId));

        ACancellationToken.ThrowIfCancellationRequested();

        long assigned;

        lock (FLock)
        {
            if (!FStreams.TryGetValue(APersistenceId, out var stream))
            {
                stream = new TStream();
                FStreams[APersistenceId] = stream;
            }

            stream.HighestSequenceNr++;
            assigned = stream.HighestSequenceNr;

            stream.Entries.Add(new TJournalEntry(
                APersistenceId,
                assigned,
                APayload,
                DateTimeOffset.UtcNow));
        }

        return Task.FromResult(assigned);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TJournalEntry> ReadAsync(
        string APersistenceId,
        long AFromSequenceNr = 0,
        [EnumeratorCancellation] CancellationToken ACancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(APersistenceId);

        TJournalEntry[] snapshot;

        lock (FLock)
        {
            if (!FStreams.TryGetValue(APersistenceId, out var stream) || stream.Entries.Count == 0)
            {
                snapshot = Array.Empty<TJournalEntry>();
            }
            else
            {
                var startIndex = 0;

                if (AFromSequenceNr > 0)
                {
                    // Stream entries are sorted by SequenceNr ascending; binary-search-equivalent
                    // is overkill for the in-memory journal — linear scan from the front is fine.
                    while (startIndex < stream.Entries.Count
                           && stream.Entries[startIndex].SequenceNr < AFromSequenceNr)
                    {
                        startIndex++;
                    }
                }

                var length = stream.Entries.Count - startIndex;

                if (length <= 0)
                {
                    snapshot = Array.Empty<TJournalEntry>();
                }
                else
                {
                    snapshot = new TJournalEntry[length];
                    stream.Entries.CopyTo(startIndex, snapshot, 0, length);
                }
            }
        }

        foreach (var entry in snapshot)
        {
            ACancellationToken.ThrowIfCancellationRequested();
            yield return entry;
        }

        await Task.CompletedTask;
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

            while (removeCount < stream.Entries.Count
                   && stream.Entries[removeCount].SequenceNr <= AToSequenceNr)
            {
                removeCount++;
            }

            if (removeCount > 0)
                stream.Entries.RemoveRange(0, removeCount);
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
        public List<TJournalEntry> Entries { get; } = new();
        public long HighestSequenceNr { get; set; }
    }
}
