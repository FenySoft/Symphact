namespace Symphact.Persistence.Tests;

/// <summary>
/// hu: A tartalom-címzett snapshot tár tesztjei: újraindítás-túlélés, deduplikáció, hash-lánc.
/// <br />
/// en: Tests for the content-addressed snapshot store: restart survival, deduplication, hash chain.
/// </summary>
public class TCasSnapshotStoreTests : IDisposable
{
    private readonly string FRoot = Path.Combine(Path.GetTempPath(), "cas-snap-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(FRoot))
        {
            Directory.Delete(FRoot, true);
        }
    }

    [Fact]
    public async Task UnknownStream_LoadIsNullAndHighestZero()
    {
        var store = new TCasSnapshotStore(FRoot);

        Assert.Null(await store.LoadAsync("nope"));
        Assert.Equal(0, await store.GetHighestSequenceNrAsync("nope"));
    }

    [Fact]
    public async Task Load_ReturnsFreshestAndRespectsUpperBound()
    {
        var store = new TCasSnapshotStore(FRoot);
        await store.SaveAsync("a", 2, "s2");
        await store.SaveAsync("a", 5, "s5");

        Assert.Equal("s5", (await store.LoadAsync("a"))!.Value.Payload);
        Assert.Equal("s2", (await store.LoadAsync("a", 4))!.Value.Payload);
        Assert.Null(await store.LoadAsync("a", 1));
    }

    [Fact]
    public async Task Snapshots_SurviveRestart()
    {
        await new TCasSnapshotStore(FRoot).SaveAsync("a", 3, "state");

        var loaded = await new TCasSnapshotStore(FRoot).LoadAsync("a");

        Assert.Equal(3, loaded!.Value.SequenceNr);
        Assert.Equal("state", loaded.Value.Payload);
    }

    [Fact]
    public async Task Save_SameSequenceNr_OverwritesPayload()
    {
        var store = new TCasSnapshotStore(FRoot);
        await store.SaveAsync("a", 1, "old");
        await store.SaveAsync("a", 1, "new");

        Assert.Equal("new", (await store.LoadAsync("a"))!.Value.Payload);
    }

    [Fact]
    public async Task Delete_RemovesUpToInclusive_AndKeepsHighest()
    {
        var store = new TCasSnapshotStore(FRoot);
        await store.SaveAsync("a", 1, "s1");
        await store.SaveAsync("a", 2, "s2");
        await store.SaveAsync("a", 3, "s3");

        await store.DeleteAsync("a", 2);

        Assert.Equal(3, (await store.LoadAsync("a"))!.Value.SequenceNr);
        Assert.Null(await store.LoadAsync("a", 2));

        await store.DeleteAsync("a", 3);

        Assert.Null(await store.LoadAsync("a"));
        Assert.Equal(3, await store.GetHighestSequenceNrAsync("a"));
    }

    [Fact]
    public async Task SamePayload_IsStoredOnce()
    {
        var store = new TCasSnapshotStore(FRoot);
        await store.SaveAsync("a", 1, "same");
        await store.SaveAsync("b", 1, "same");

        Assert.Single(Directory.GetFiles(Path.Combine(FRoot, "objects")));
    }

    [Fact]
    public async Task TamperedBlob_IsDetectedOnLoad()
    {
        var store = new TCasSnapshotStore(FRoot);
        await store.SaveAsync("a", 1, "secret");
        File.WriteAllBytes(Directory.GetFiles(Path.Combine(FRoot, "objects")).Single(), new byte[] { 1 });

        await Assert.ThrowsAsync<InvalidDataException>(async () => await store.LoadAsync("a"));
    }

    [Fact]
    public async Task SwappedPayloadHash_IsDetectedByChain()
    {
        var store = new TCasSnapshotStore(FRoot);
        await store.SaveAsync("a", 1, "one");
        await store.SaveAsync("a", 2, "two");
        var index = Directory.GetFiles(Path.Combine(FRoot, "snapshots")).Single();
        var lines = File.ReadAllLines(index);
        var first = lines[0].Split(' ');
        first[3] = lines[1].Split(' ')[3];
        File.WriteAllLines(index, new[] { string.Join(' ', first), lines[1] });

        await Assert.ThrowsAsync<InvalidDataException>(async () => await store.LoadAsync("a"));
    }

    [Fact]
    public async Task InvalidArguments_Throw()
    {
        var store = new TCasSnapshotStore(FRoot);

        await Assert.ThrowsAsync<ArgumentException>(async () => await store.SaveAsync("", 1, "x"));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await store.SaveAsync("a", 1, null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await store.SaveAsync("a", 0, "x"));
    }
}
