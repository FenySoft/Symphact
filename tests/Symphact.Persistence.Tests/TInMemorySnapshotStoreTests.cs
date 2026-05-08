namespace Symphact.Persistence.Tests;

/// <summary>
/// hu: Az in-memory snapshot store viselkedési tesztjei. A snapshot store az event sourcing
/// recovery útvonal másik fele (a journal mellett): a friss állapotot pillanatképben tartja,
/// hogy a recovery ne kelljen az egész journalt visszajátssza.
/// <br />
/// en: Behavioural tests for the in-memory snapshot store. The snapshot store is the other
/// half of the event sourcing recovery path (alongside the journal): it holds a fresh state
/// snapshot so recovery does not have to replay the entire journal.
/// </summary>
public class TInMemorySnapshotStoreTests
{
    [Fact]
    public async Task EmptyStore_LoadReturnsNull()
    {
        var store = new TInMemorySnapshotStore();

        var snapshot = await store.LoadAsync("unknown");

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task EmptyStore_HighestSequenceIsZero()
    {
        var store = new TInMemorySnapshotStore();

        var highest = await store.GetHighestSequenceNrAsync("unknown");

        Assert.Equal(0, highest);
    }

    [Fact]
    public async Task Save_StoresSnapshotAndLoadReturnsIt()
    {
        var store = new TInMemorySnapshotStore();

        await store.SaveAsync("a", 5, "state-at-5");

        var snapshot = await store.LoadAsync("a");

        Assert.NotNull(snapshot);
        Assert.Equal("a", snapshot.Value.PersistenceId);
        Assert.Equal(5, snapshot.Value.SequenceNr);
        Assert.Equal("state-at-5", snapshot.Value.Payload);
    }

    [Fact]
    public async Task Save_MultipleSnapshots_LoadReturnsHighestSequence()
    {
        var store = new TInMemorySnapshotStore();

        await store.SaveAsync("a", 1, "state-1");
        await store.SaveAsync("a", 5, "state-5");
        await store.SaveAsync("a", 3, "state-3");

        var snapshot = await store.LoadAsync("a");

        Assert.NotNull(snapshot);
        Assert.Equal(5, snapshot.Value.SequenceNr);
        Assert.Equal("state-5", snapshot.Value.Payload);
    }

    [Fact]
    public async Task Load_WithMaxSequenceNr_ReturnsHighestUpToBound()
    {
        var store = new TInMemorySnapshotStore();

        await store.SaveAsync("a", 1, "state-1");
        await store.SaveAsync("a", 3, "state-3");
        await store.SaveAsync("a", 7, "state-7");

        var snapshot = await store.LoadAsync("a", AMaxSequenceNr: 5);

        Assert.NotNull(snapshot);
        Assert.Equal(3, snapshot.Value.SequenceNr);
        Assert.Equal("state-3", snapshot.Value.Payload);
    }

    [Fact]
    public async Task Load_WithMaxSequenceNrBelowAll_ReturnsNull()
    {
        var store = new TInMemorySnapshotStore();

        await store.SaveAsync("a", 5, "state-5");
        await store.SaveAsync("a", 9, "state-9");

        var snapshot = await store.LoadAsync("a", AMaxSequenceNr: 4);

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task Save_OverwritesExistingSequenceNr()
    {
        var store = new TInMemorySnapshotStore();

        await store.SaveAsync("a", 5, "first");
        await store.SaveAsync("a", 5, "second");

        var snapshot = await store.LoadAsync("a");

        Assert.NotNull(snapshot);
        Assert.Equal(5, snapshot.Value.SequenceNr);
        Assert.Equal("second", snapshot.Value.Payload);
    }

    [Fact]
    public async Task Save_DifferentPersistenceIds_AreIsolated()
    {
        var store = new TInMemorySnapshotStore();

        await store.SaveAsync("a", 1, "a-state");
        await store.SaveAsync("b", 1, "b-state");
        await store.SaveAsync("a", 2, "a-state-2");

        var a = await store.LoadAsync("a");
        var b = await store.LoadAsync("b");

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal("a-state-2", a.Value.Payload);
        Assert.Equal(2, a.Value.SequenceNr);
        Assert.Equal("b-state", b.Value.Payload);
        Assert.Equal(1, b.Value.SequenceNr);
    }

    [Fact]
    public async Task Save_UpdatesHighestSequenceNr()
    {
        var store = new TInMemorySnapshotStore();

        await store.SaveAsync("a", 3, "x");
        await store.SaveAsync("a", 7, "y");

        var highest = await store.GetHighestSequenceNrAsync("a");

        Assert.Equal(7, highest);
    }

    [Fact]
    public async Task Save_HighestSequenceNrTracksMaximum_NotLastWrite()
    {
        var store = new TInMemorySnapshotStore();

        await store.SaveAsync("a", 7, "y");
        await store.SaveAsync("a", 3, "x");

        var highest = await store.GetHighestSequenceNrAsync("a");

        Assert.Equal(7, highest);
    }

    [Fact]
    public async Task Delete_RemovesSnapshotsUpToSequenceNr()
    {
        var store = new TInMemorySnapshotStore();

        await store.SaveAsync("a", 1, "s1");
        await store.SaveAsync("a", 3, "s3");
        await store.SaveAsync("a", 5, "s5");

        await store.DeleteAsync("a", AToSequenceNr: 3);

        var snapshot = await store.LoadAsync("a", AMaxSequenceNr: 4);

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task Delete_DoesNotAffectNewerSnapshots()
    {
        var store = new TInMemorySnapshotStore();

        await store.SaveAsync("a", 1, "s1");
        await store.SaveAsync("a", 3, "s3");
        await store.SaveAsync("a", 5, "s5");

        await store.DeleteAsync("a", AToSequenceNr: 3);

        var snapshot = await store.LoadAsync("a");

        Assert.NotNull(snapshot);
        Assert.Equal(5, snapshot.Value.SequenceNr);
        Assert.Equal("s5", snapshot.Value.Payload);
    }

    [Fact]
    public async Task Delete_PreservesHighestSequenceNr()
    {
        var store = new TInMemorySnapshotStore();

        await store.SaveAsync("a", 1, "s1");
        await store.SaveAsync("a", 3, "s3");
        await store.SaveAsync("a", 5, "s5");

        await store.DeleteAsync("a", AToSequenceNr: 5);

        var highest = await store.GetHighestSequenceNrAsync("a");

        Assert.Equal(5, highest);
    }

    [Fact]
    public async Task Delete_OnUnknownStream_DoesNotThrow()
    {
        var store = new TInMemorySnapshotStore();

        await store.DeleteAsync("nope", AToSequenceNr: 5);

        Assert.Equal(0, await store.GetHighestSequenceNrAsync("nope"));
        Assert.Null(await store.LoadAsync("nope"));
    }

    [Fact]
    public async Task Delete_AllSnapshots_LoadReturnsNull()
    {
        var store = new TInMemorySnapshotStore();

        await store.SaveAsync("a", 3, "s3");
        await store.SaveAsync("a", 7, "s7");

        await store.DeleteAsync("a", AToSequenceNr: 7);

        var snapshot = await store.LoadAsync("a");

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task Save_NullPersistenceId_Throws()
    {
        var store = new TInMemorySnapshotStore();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            store.SaveAsync(null!, 1, "payload"));
    }

    [Fact]
    public async Task Save_EmptyPersistenceId_Throws()
    {
        var store = new TInMemorySnapshotStore();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(string.Empty, 1, "payload"));
    }

    [Fact]
    public async Task Save_NullPayload_Throws()
    {
        var store = new TInMemorySnapshotStore();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            store.SaveAsync("a", 1, null!));
    }

    [Fact]
    public async Task Save_NonPositiveSequenceNr_Throws()
    {
        var store = new TInMemorySnapshotStore();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.SaveAsync("a", 0, "payload"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.SaveAsync("a", -1, "payload"));
    }

    [Fact]
    public async Task Save_HonoursCancellation()
    {
        var store = new TInMemorySnapshotStore();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.SaveAsync("a", 1, "payload", cts.Token));
    }

    [Fact]
    public async Task Load_HonoursCancellation()
    {
        var store = new TInMemorySnapshotStore();
        await store.SaveAsync("a", 1, "payload");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.LoadAsync("a", AMaxSequenceNr: long.MaxValue, ACancellationToken: cts.Token));
    }

    [Fact]
    public async Task Delete_HonoursCancellation()
    {
        var store = new TInMemorySnapshotStore();
        await store.SaveAsync("a", 1, "payload");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.DeleteAsync("a", AToSequenceNr: 1, ACancellationToken: cts.Token));
    }

    [Fact]
    public async Task Snapshot_CarriesTimestamp()
    {
        var store = new TInMemorySnapshotStore();

        var before = DateTimeOffset.UtcNow;
        await store.SaveAsync("a", 1, "x");
        var after = DateTimeOffset.UtcNow;

        var snapshot = await store.LoadAsync("a");

        Assert.NotNull(snapshot);
        Assert.InRange(snapshot.Value.Timestamp, before, after);
    }

    [Fact]
    public async Task ConcurrentSavesToDifferentStreams_DoNotInterfere()
    {
        var store = new TInMemorySnapshotStore();

        const int streams = 16;
        const int snapshotsPerStream = 50;

        var tasks = Enumerable.Range(0, streams)
            .Select(s => Task.Run(async () =>
            {
                var id = $"stream-{s}";

                for (var i = 1; i <= snapshotsPerStream; i++)
                    await store.SaveAsync(id, i, $"state-{s}-{i}");
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        for (var s = 0; s < streams; s++)
        {
            var id = $"stream-{s}";
            var highest = await store.GetHighestSequenceNrAsync(id);
            var latest = await store.LoadAsync(id);

            Assert.Equal(snapshotsPerStream, highest);
            Assert.NotNull(latest);
            Assert.Equal(snapshotsPerStream, latest.Value.SequenceNr);
            Assert.Equal($"state-{s}-{snapshotsPerStream}", latest.Value.Payload);
        }
    }

    [Fact]
    public async Task ConcurrentSavesToSameStream_AreThreadSafe()
    {
        var store = new TInMemorySnapshotStore();

        const int writers = 8;
        const int snapshotsPerWriter = 100;

        var tasks = Enumerable.Range(0, writers)
            .Select(w => Task.Run(async () =>
            {
                for (var i = 1; i <= snapshotsPerWriter; i++)
                {
                    var seq = (long)(w * snapshotsPerWriter + i);

                    await store.SaveAsync("shared", seq, $"w{w}-{i}");
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        var highest = await store.GetHighestSequenceNrAsync("shared");
        var latest = await store.LoadAsync("shared");

        Assert.Equal(writers * snapshotsPerWriter, highest);
        Assert.NotNull(latest);
        Assert.Equal(writers * snapshotsPerWriter, latest.Value.SequenceNr);
    }
}
