namespace Symphact.Persistence.Tests;

/// <summary>
/// hu: A tartalom-címzett journal tesztjei: újraindítás-túlélés, deduplikáció, hash-lánc.
/// <br />
/// en: Tests for the content-addressed journal: restart survival, deduplication, hash chain.
/// </summary>
public class TCasJournalTests : IDisposable
{
    private readonly string FRoot = Path.Combine(Path.GetTempPath(), "cas-journal-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(FRoot))
        {
            Directory.Delete(FRoot, true);
        }
    }

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
    public async Task Append_AssignsMonotonicSequenceStartingAtOne()
    {
        var journal = new TCasJournal(FRoot);

        Assert.Equal(1, await journal.AppendAsync("a", "first"));
        Assert.Equal(2, await journal.AppendAsync("a", "second"));
        Assert.Equal(2, await journal.GetHighestSequenceNrAsync("a"));
    }

    [Fact]
    public async Task Entries_SurviveRestart()
    {
        var first = new TCasJournal(FRoot);
        await first.AppendAsync("a", "one");
        await first.AppendAsync("a", "two");

        var second = new TCasJournal(FRoot);
        var entries = await CollectAsync(second.ReadAsync("a"));

        Assert.Equal(new object[] { "one", "two" }, entries.Select(e => e.Payload).ToArray());
        Assert.Equal(new long[] { 1, 2 }, entries.Select(e => e.SequenceNr).ToArray());
        Assert.Equal(3, await second.AppendAsync("a", "three"));
    }

    [Fact]
    public async Task Read_FromSequenceNr_IsInclusive()
    {
        var journal = new TCasJournal(FRoot);
        await journal.AppendAsync("a", "x");
        await journal.AppendAsync("a", "y");
        await journal.AppendAsync("a", "z");

        var entries = await CollectAsync(journal.ReadAsync("a", 2));

        Assert.Equal(new long[] { 2, 3 }, entries.Select(e => e.SequenceNr).ToArray());
    }

    [Fact]
    public async Task Delete_KeepsHighestSequenceNr()
    {
        var journal = new TCasJournal(FRoot);
        await journal.AppendAsync("a", "x");
        await journal.AppendAsync("a", "y");

        await journal.DeleteAsync("a", 2);

        Assert.Empty(await CollectAsync(journal.ReadAsync("a")));
        Assert.Equal(2, await journal.GetHighestSequenceNrAsync("a"));
        Assert.Equal(3, await new TCasJournal(FRoot).AppendAsync("a", "z"));
    }

    [Fact]
    public async Task UnknownStream_IsEmpty()
    {
        var journal = new TCasJournal(FRoot);

        Assert.Equal(0, await journal.GetHighestSequenceNrAsync("nope"));
        Assert.Empty(await CollectAsync(journal.ReadAsync("nope")));
    }

    [Fact]
    public async Task SamePayload_IsStoredOnce()
    {
        var journal = new TCasJournal(FRoot);

        await journal.AppendAsync("a", "same");
        await journal.AppendAsync("b", "same");

        Assert.Single(Directory.GetFiles(Path.Combine(FRoot, "objects")));
    }

    [Fact]
    public async Task TamperedBlob_IsDetectedOnRead()
    {
        var journal = new TCasJournal(FRoot);
        await journal.AppendAsync("a", "secret");
        var blob = Directory.GetFiles(Path.Combine(FRoot, "objects")).Single();
        File.WriteAllBytes(blob, new byte[] { 1, 2, 3 });

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await CollectAsync(journal.ReadAsync("a")));
    }

    [Fact]
    public async Task TamperedIndex_IsDetectedOnRead()
    {
        var journal = new TCasJournal(FRoot);
        await journal.AppendAsync("a", "one");
        await journal.AppendAsync("a", "two");
        var index = Directory.GetFiles(Path.Combine(FRoot, "streams")).Single();
        var lines = File.ReadAllLines(index);
        File.WriteAllLines(index, new[] { lines[1], lines[0] });

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await CollectAsync(journal.ReadAsync("a")));
    }

    [Fact]
    public async Task SwappedPayloadHash_IsDetectedByChain()
    {
        var journal = new TCasJournal(FRoot);
        await journal.AppendAsync("a", "one");
        await journal.AppendAsync("a", "two");
        var index = Directory.GetFiles(Path.Combine(FRoot, "streams")).Single();
        var lines = File.ReadAllLines(index);
        var first = lines[0].Split(' ');
        first[3] = lines[1].Split(' ')[3];
        File.WriteAllLines(index, new[] { string.Join(' ', first), lines[1] });

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await CollectAsync(journal.ReadAsync("a")));
    }

    [Fact]
    public async Task NullOrEmptyArguments_Throw()
    {
        var journal = new TCasJournal(FRoot);

        await Assert.ThrowsAsync<ArgumentException>(async () => await journal.AppendAsync("", "x"));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await journal.AppendAsync("a", null!));
    }
}
