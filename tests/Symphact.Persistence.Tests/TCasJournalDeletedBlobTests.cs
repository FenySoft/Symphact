using System.Security.Cryptography;

namespace Symphact.Persistence.Tests;

/// <summary>
/// hu: A journal olvasása a törölt bejegyzések blobja nélkül is ép.
/// <br />
/// en: Journal reading stays intact without the blobs of deleted entries.
/// </summary>
public class TCasJournalDeletedBlobTests : IDisposable
{
    private readonly string FRoot = Path.Combine(Path.GetTempPath(), "cas-journal-del-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(FRoot))
        {
            Directory.Delete(FRoot, true);
        }
    }

    [Fact]
    public async Task Read_DoesNotNeedTheBlobOfADeletedEntry()
    {
        var journal = new TCasJournal(FRoot);
        await journal.AppendAsync("a", "old");
        await journal.AppendAsync("a", "live");
        await journal.DeleteAsync("a", 1);

        File.Delete(Path.Combine(FRoot, "objects", Convert.ToHexString(SHA256.HashData(
            new TJsonPayloadSerializer().Serialize("old"))).ToLowerInvariant()));

        var entries = new List<TJournalEntry>();

        await foreach (var e in journal.ReadAsync("a"))
        {
            entries.Add(e);
        }

        Assert.Equal(new object[] { "live" }, entries.Select(e => e.Payload).ToArray());
        Assert.Equal(3, await journal.AppendAsync("a", "next"));
    }
}
