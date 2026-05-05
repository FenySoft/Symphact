namespace Symphact.Persistence.Tests;

/// <summary>
/// hu: Az in-memory journal viselkedési tesztjei. A Journal a Symphact perzisztencia
/// HAL-jának BCL-only referencia implementációja — nincs NuGet függősége, és a Core OS
/// drop-in célja miatt minimális API felület.
/// <br />
/// en: Behavioural tests for the in-memory journal. The journal is the BCL-only reference
/// implementation of the Symphact persistence HAL — no NuGet dependencies, minimal API
/// surface for the Core OS drop-in target.
/// </summary>
public class TInMemoryJournalTests
{
    [Fact]
    public async Task EmptyStream_HighestSequenceIsZero()
    {
        var journal = new TInMemoryJournal();

        var highest = await journal.GetHighestSequenceNrAsync("unknown");

        Assert.Equal(0, highest);
    }

    [Fact]
    public async Task EmptyStream_ReadYieldsNothing()
    {
        var journal = new TInMemoryJournal();

        var entries = await CollectAsync(journal.ReadAsync("unknown"));

        Assert.Empty(entries);
    }

    [Fact]
    public async Task Append_AssignsMonotonicSequenceStartingAtOne()
    {
        var journal = new TInMemoryJournal();

        var s1 = await journal.AppendAsync("a", "first");
        var s2 = await journal.AppendAsync("a", "second");
        var s3 = await journal.AppendAsync("a", "third");

        Assert.Equal(1, s1);
        Assert.Equal(2, s2);
        Assert.Equal(3, s3);
    }

    [Fact]
    public async Task Append_UpdatesHighestSequenceNr()
    {
        var journal = new TInMemoryJournal();

        await journal.AppendAsync("a", "x");
        await journal.AppendAsync("a", "y");

        var highest = await journal.GetHighestSequenceNrAsync("a");

        Assert.Equal(2, highest);
    }

    [Fact]
    public async Task Read_ReturnsEntriesInOrder()
    {
        var journal = new TInMemoryJournal();

        await journal.AppendAsync("a", "first");
        await journal.AppendAsync("a", "second");
        await journal.AppendAsync("a", "third");

        var entries = await CollectAsync(journal.ReadAsync("a"));

        Assert.Equal(3, entries.Count);
        Assert.Equal("first", entries[0].Payload);
        Assert.Equal("second", entries[1].Payload);
        Assert.Equal("third", entries[2].Payload);
        Assert.Equal(1, entries[0].SequenceNr);
        Assert.Equal(2, entries[1].SequenceNr);
        Assert.Equal(3, entries[2].SequenceNr);
    }

    [Fact]
    public async Task Read_FromSequenceNr_SkipsEarlierEntries()
    {
        var journal = new TInMemoryJournal();

        await journal.AppendAsync("a", "first");
        await journal.AppendAsync("a", "second");
        await journal.AppendAsync("a", "third");

        var entries = await CollectAsync(journal.ReadAsync("a", AFromSequenceNr: 2));

        Assert.Equal(2, entries.Count);
        Assert.Equal("second", entries[0].Payload);
        Assert.Equal("third", entries[1].Payload);
    }

    [Fact]
    public async Task Read_FromSequenceNrBeyondEnd_ReturnsEmpty()
    {
        var journal = new TInMemoryJournal();

        await journal.AppendAsync("a", "first");

        var entries = await CollectAsync(journal.ReadAsync("a", AFromSequenceNr: 99));

        Assert.Empty(entries);
    }

    [Fact]
    public async Task DifferentPersistenceIds_AreIsolated()
    {
        var journal = new TInMemoryJournal();

        var a1 = await journal.AppendAsync("a", "ax");
        var b1 = await journal.AppendAsync("b", "bx");
        var a2 = await journal.AppendAsync("a", "ay");

        Assert.Equal(1, a1);
        Assert.Equal(1, b1);
        Assert.Equal(2, a2);

        var aEntries = await CollectAsync(journal.ReadAsync("a"));
        var bEntries = await CollectAsync(journal.ReadAsync("b"));

        Assert.Equal(2, aEntries.Count);
        Assert.Single(bEntries);
        Assert.Equal("bx", bEntries[0].Payload);
    }

    [Fact]
    public async Task Delete_RemovesEntriesUpToSequenceNr()
    {
        var journal = new TInMemoryJournal();

        await journal.AppendAsync("a", "1");
        await journal.AppendAsync("a", "2");
        await journal.AppendAsync("a", "3");

        await journal.DeleteAsync("a", AToSequenceNr: 2);

        var entries = await CollectAsync(journal.ReadAsync("a"));

        Assert.Single(entries);
        Assert.Equal("3", entries[0].Payload);
        Assert.Equal(3, entries[0].SequenceNr);
    }

    [Fact]
    public async Task Delete_PreservesHighestSequenceNr()
    {
        var journal = new TInMemoryJournal();

        await journal.AppendAsync("a", "1");
        await journal.AppendAsync("a", "2");
        await journal.AppendAsync("a", "3");

        await journal.DeleteAsync("a", AToSequenceNr: 3);

        var highest = await journal.GetHighestSequenceNrAsync("a");
        var nextSeq = await journal.AppendAsync("a", "4");

        Assert.Equal(3, highest);
        Assert.Equal(4, nextSeq);
    }

    [Fact]
    public async Task Delete_OnUnknownStream_DoesNotThrow()
    {
        var journal = new TInMemoryJournal();

        await journal.DeleteAsync("nope", AToSequenceNr: 5);

        Assert.Equal(0, await journal.GetHighestSequenceNrAsync("nope"));
    }

    [Fact]
    public async Task Append_NullPersistenceId_Throws()
    {
        var journal = new TInMemoryJournal();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            journal.AppendAsync(null!, "payload"));
    }

    [Fact]
    public async Task Append_NullPayload_Throws()
    {
        var journal = new TInMemoryJournal();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            journal.AppendAsync("a", null!));
    }

    [Fact]
    public async Task Append_EmptyPersistenceId_Throws()
    {
        var journal = new TInMemoryJournal();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            journal.AppendAsync(string.Empty, "payload"));
    }

    [Fact]
    public async Task Read_HonoursCancellation()
    {
        var journal = new TInMemoryJournal();
        await journal.AppendAsync("a", "1");
        await journal.AppendAsync("a", "2");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in journal.ReadAsync("a", 0, cts.Token))
            {
            }
        });
    }

    [Fact]
    public async Task ConcurrentAppends_PreserveMonotonicSequencePerStream()
    {
        var journal = new TInMemoryJournal();

        const int writers = 8;
        const int messagesPerWriter = 250;

        var tasks = Enumerable.Range(0, writers)
            .Select(_ => Task.Run(async () =>
            {
                for (var i = 0; i < messagesPerWriter; i++)
                    await journal.AppendAsync("stream", i);
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        var entries = await CollectAsync(journal.ReadAsync("stream"));

        Assert.Equal(writers * messagesPerWriter, entries.Count);

        for (var i = 0; i < entries.Count; i++)
            Assert.Equal(i + 1, entries[i].SequenceNr);
    }

    [Fact]
    public async Task ConcurrentAppendsToDifferentStreams_DoNotInterfere()
    {
        var journal = new TInMemoryJournal();

        const int streams = 16;
        const int messages = 100;

        var tasks = Enumerable.Range(0, streams)
            .Select(s => Task.Run(async () =>
            {
                var id = $"stream-{s}";

                for (var i = 0; i < messages; i++)
                    await journal.AppendAsync(id, i);
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        for (var s = 0; s < streams; s++)
        {
            var id = $"stream-{s}";
            var highest = await journal.GetHighestSequenceNrAsync(id);

            Assert.Equal(messages, highest);
        }
    }

    [Fact]
    public async Task JournalEntry_CarriesTimestamp()
    {
        var journal = new TInMemoryJournal();

        var before = DateTimeOffset.UtcNow;
        await journal.AppendAsync("a", "x");
        var after = DateTimeOffset.UtcNow;

        var entries = await CollectAsync(journal.ReadAsync("a"));

        Assert.Single(entries);
        Assert.InRange(entries[0].Timestamp, before, after);
    }

    [Fact]
    public async Task JournalEntry_CarriesPersistenceId()
    {
        var journal = new TInMemoryJournal();

        await journal.AppendAsync("alpha", "x");

        var entries = await CollectAsync(journal.ReadAsync("alpha"));

        Assert.Single(entries);
        Assert.Equal("alpha", entries[0].PersistenceId);
    }

    private static async Task<List<TJournalEntry>> CollectAsync(IAsyncEnumerable<TJournalEntry> ASource)
    {
        var list = new List<TJournalEntry>();

        await foreach (var entry in ASource)
            list.Add(entry);

        return list;
    }
}
