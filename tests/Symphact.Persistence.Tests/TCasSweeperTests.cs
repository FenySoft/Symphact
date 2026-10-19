namespace Symphact.Persistence.Tests;

/// <summary>
/// hu: A tartalom-címzett tárak takarítójának tesztjei: a nem hivatkozott blob eltűnik, a
/// hivatkozott marad, a törölt bejegyzések mellett az olvasás és az írás ép marad.
/// <br />
/// en: Tests for the content-addressed store sweeper: an unreferenced blob disappears, a
/// referenced one stays, and reading and writing stay intact next to deleted entries.
/// </summary>
public class TCasSweeperTests : IDisposable
{
    private readonly string FRoot = Path.Combine(Path.GetTempPath(), "cas-sweep-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(FRoot))
        {
            Directory.Delete(FRoot, true);
        }
    }

    private string[] Blobs() => Directory.GetFiles(Path.Combine(FRoot, "objects")).Select(Path.GetFileName).Cast<string>().ToArray();

    private static async Task<List<TJournalEntry>> CollectAsync(IAsyncEnumerable<TJournalEntry> AEntries)
    {
        var list = new List<TJournalEntry>();

        await foreach (var e in AEntries)
        {
            list.Add(e);
        }

        return list;
    }

    [Fact]
    public async Task DeletedJournalEntry_BlobIsRemoved_LiveBlobStays()
    {
        var journal = new TCasJournal(FRoot);
        await journal.AppendAsync("a", "old");
        await journal.AppendAsync("a", "live");
        await journal.DeleteAsync("a", 1);

        var removed = await TCasSweeper.SweepAsync(FRoot);

        Assert.Equal(1, removed);
        Assert.Single(Blobs());
        Assert.Equal(new object[] { "live" }, (await CollectAsync(journal.ReadAsync("a"))).Select(e => e.Payload).ToArray());
    }

    [Fact]
    public async Task Journal_RemainsAppendableAndReadable_AfterSweep()
    {
        var journal = new TCasJournal(FRoot);
        await journal.AppendAsync("a", "old");
        await journal.DeleteAsync("a", 1);
        await TCasSweeper.SweepAsync(FRoot);

        var restarted = new TCasJournal(FRoot);

        Assert.Equal(2, await restarted.AppendAsync("a", "new"));
        Assert.Equal(new object[] { "new" }, (await CollectAsync(restarted.ReadAsync("a"))).Select(e => e.Payload).ToArray());
    }

    [Fact]
    public async Task DeletedSnapshot_BlobIsRemoved_LiveBlobStays()
    {
        var store = new TCasSnapshotStore(FRoot);
        await store.SaveAsync("a", 1, "old");
        await store.SaveAsync("a", 2, "live");
        await store.DeleteAsync("a", 1);

        var removed = await TCasSweeper.SweepAsync(FRoot);

        Assert.Equal(1, removed);
        Assert.Equal("live", (await store.LoadAsync("a"))!.Value.Payload);
    }

    [Fact]
    public async Task BlobSharedAcrossJournalAndSnapshot_StaysWhileEitherLivesOnIt()
    {
        var journal = new TCasJournal(FRoot);
        var snapshots = new TCasSnapshotStore(FRoot);
        await journal.AppendAsync("a", "shared");
        await snapshots.SaveAsync("a", 1, "shared");
        await journal.DeleteAsync("a", 1);

        Assert.Equal(0, await TCasSweeper.SweepAsync(FRoot));
        Assert.Equal("shared", (await snapshots.LoadAsync("a"))!.Value.Payload);

        await snapshots.DeleteAsync("a", 1);

        Assert.Equal(1, await TCasSweeper.SweepAsync(FRoot));
        Assert.Empty(Blobs());
    }

    [Fact]
    public async Task BlobSharedAcrossStreams_StaysWhileOneStreamLivesOnIt()
    {
        var journal = new TCasJournal(FRoot);
        await journal.AppendAsync("a", "shared");
        await journal.AppendAsync("b", "shared");
        await journal.DeleteAsync("a", 1);

        Assert.Equal(0, await TCasSweeper.SweepAsync(FRoot));
        Assert.Equal(new object[] { "shared" }, (await CollectAsync(journal.ReadAsync("b"))).Select(e => e.Payload).ToArray());
    }

    [Fact]
    public async Task OrphanTempFile_IsRemoved()
    {
        var journal = new TCasJournal(FRoot);
        await journal.AppendAsync("a", "live");
        await File.WriteAllTextAsync(Path.Combine(FRoot, "objects", "abc.def.tmp"), "partial");

        var removed = await TCasSweeper.SweepAsync(FRoot);

        Assert.Equal(1, removed);
        Assert.Single(Blobs());
    }

    [Fact]
    public async Task SweepOfUnknownRoot_RemovesNothing()
    {
        Assert.Equal(0, await TCasSweeper.SweepAsync(FRoot));
    }

    [Fact]
    public async Task ConcurrentAppendsAndSweeps_NeverLeaveDanglingBlob()
    {
        var journal = new TCasJournal(FRoot);
        await journal.AppendAsync("a", "seed");
        await journal.DeleteAsync("a", 1);

        var writers = Enumerable.Range(0, 40).Select(i => Task.Run(async () =>
        {
            await journal.AppendAsync("s" + (i % 4), "p" + (i % 5));
        })).ToList();
        var sweepers = Enumerable.Range(0, 20).Select(_ => Task.Run(() => TCasSweeper.SweepAsync(FRoot))).ToList();

        await Task.WhenAll(writers.Concat(sweepers));

        for (var s = 0; s < 4; s++)
        {
            Assert.Equal(10, (await CollectAsync(journal.ReadAsync("s" + s))).Count);
        }
    }
}
