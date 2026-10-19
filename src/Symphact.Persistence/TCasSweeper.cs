using System.Globalization;

namespace Symphact.Persistence;

/// <summary>
/// hu: Takarító a tartalom-címzett tárakhoz (mark-and-sweep): az összes index-fájlból
/// (<c>streams/</c> és <c>snapshots/</c>) újraszámolja az élő blobokat, a többit törli az
/// <c>objects/</c> könyvtárból, az összeomlásból maradt <c>.tmp</c> fájlokkal együtt. A takarítás
/// a gyökéren kizárja az írást (folyamaton belül és folyamatok között). Az index-fájlok
/// (törlés-jelölők és törölt bejegyzések sorai) megmaradnak: a hash-lánc ezt igényli.
/// <br />
/// en: Sweeper for the content-addressed stores (mark-and-sweep): recomputes the live blobs from
/// all index files (<c>streams/</c> and <c>snapshots/</c>) and deletes the rest from
/// <c>objects/</c>, including <c>.tmp</c> files left by a crash. Sweeping excludes writes on the
/// root (in-process and cross-process). The index files (delete markers and deleted entry lines)
/// stay: the hash chain requires them.
/// </summary>
public static class TCasSweeper
{
    /// <summary>
    /// hu: Törli az objects/ azon fájljait, amelyekre nincs élő hivatkozás.
    /// <br />
    /// en: Deletes the objects/ files that have no live reference.
    /// </summary>
    /// <param name="ARootDirectory">
    /// hu: A tár gyökérkönyvtára.
    /// <br />
    /// en: The store root directory.
    /// </param>
    /// <param name="ACancellationToken">
    /// hu: Megszakító token.
    /// <br />
    /// en: Cancellation token.
    /// </param>
    /// <returns>
    /// hu: A törölt fájlok száma.
    /// <br />
    /// en: Number of deleted files.
    /// </returns>
    public static async Task<int> SweepAsync(string ARootDirectory, CancellationToken ACancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(ARootDirectory);

        using var root = await TCasFiles.EnterRootAsync(ARootDirectory, ACancellationToken).ConfigureAwait(false);

        var objects = Path.Combine(ARootDirectory, TCasFiles.ObjectsDirectoryName);

        if (!Directory.Exists(objects))
        {
            return 0;
        }

        var live = new HashSet<string>(StringComparer.Ordinal);

        foreach (var directory in new[] { TCasFiles.StreamsDirectoryName, TCasFiles.SnapshotsDirectoryName })
        {
            var path = Path.Combine(ARootDirectory, directory);

            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var index in Directory.GetFiles(path, "*.log"))
            {
                MarkLive(index, live);
            }
        }

        var removed = 0;

        foreach (var file in Directory.GetFiles(objects))
        {
            ACancellationToken.ThrowIfCancellationRequested();

            if (live.Contains(Path.GetFileName(file)))
            {
                continue;
            }

            File.Delete(file);
            removed++;
        }

        return removed;
    }

    private static void MarkLive(string AIndexPath, HashSet<string> ALive)
    {
        var entries = new Dictionary<long, string>();

        foreach (var line in File.ReadAllLines(AIndexPath))
        {
            var parts = line.Split(' ');

            if (parts[0] == "D" && parts.Length == 2)
            {
                var to = long.Parse(parts[1], CultureInfo.InvariantCulture);

                foreach (var key in entries.Keys.Where(k => k <= to).ToList())
                {
                    entries.Remove(key);
                }

                continue;
            }

            if ((parts[0] != "E" && parts[0] != "S") || parts.Length != 5)
            {
                throw new InvalidDataException("Malformed index line.");
            }

            entries[long.Parse(parts[1], CultureInfo.InvariantCulture)] = parts[3];
        }

        foreach (var hash in entries.Values)
        {
            ALive.Add(hash);
        }
    }
}
