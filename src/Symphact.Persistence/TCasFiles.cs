using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Symphact.Persistence;

/// <summary>
/// hu: A tartalom-címzett tárak közös fájlrendszeres alapja: blob-írás/olvasás SHA-256
/// ellenőrzéssel és index-sor hozzáfűzés fsync-kel.
/// <br />
/// en: Shared file-system base of the content-addressed stores: blob write/read with SHA-256
/// verification and index-line append with fsync.
/// </summary>
internal sealed class TCasFiles
{
    public const string ObjectsDirectoryName = "objects";
    public const string StreamsDirectoryName = "streams";
    public const string SnapshotsDirectoryName = "snapshots";
    private const string LockFileName = ".sweep.lock";

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RootLocks = new();

    public const string Genesis = "0000000000000000000000000000000000000000000000000000000000000000";

    private readonly string FObjects;
    private readonly string FRoot;
    private readonly string FIndexDirectory;
    private readonly IPayloadSerializer FSerializer;

    public TCasFiles(string ARootDirectory, string AIndexDirectoryName, IPayloadSerializer? ASerializer)
    {
        ArgumentException.ThrowIfNullOrEmpty(ARootDirectory);

        FObjects = Path.Combine(ARootDirectory, ObjectsDirectoryName);
        FRoot = Path.GetFullPath(ARootDirectory);
        FIndexDirectory = Path.Combine(ARootDirectory, AIndexDirectoryName);
        FSerializer = ASerializer ?? new TJsonPayloadSerializer();
        Directory.CreateDirectory(FObjects);
        Directory.CreateDirectory(FIndexDirectory);
    }

    /// <summary>
    /// hu: Kizárólagos belépés a gyökérre (folyamaton belül és folyamatok között): az írás és a
    /// takarítás egymást kizárja.
    /// <br />
    /// en: Exclusive entry to the root (in-process and cross-process): writing and sweeping
    /// exclude each other.
    /// </summary>
    public static async Task<IDisposable> EnterRootAsync(string ARootDirectory, CancellationToken ACancellationToken)
    {
        var root = Path.GetFullPath(ARootDirectory);
        var gate = RootLocks.GetOrAdd(root, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(ACancellationToken).ConfigureAwait(false);

        try
        {
            Directory.CreateDirectory(root);

            while (true)
            {
                try
                {
                    var file = new FileStream(Path.Combine(root, LockFileName), FileMode.OpenOrCreate,
                        FileAccess.ReadWrite, FileShare.None);

                    return new TRootLease(gate, file);
                }
                catch (IOException)
                {
                    await Task.Delay(5, ACancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch
        {
            gate.Release();

            throw;
        }
    }

    public Task<IDisposable> EnterRootAsync(CancellationToken ACancellationToken)
        => EnterRootAsync(FRoot, ACancellationToken);

    private sealed class TRootLease(SemaphoreSlim AGate, FileStream AFile) : IDisposable
    {
        public void Dispose()
        {
            AFile.Dispose();
            AGate.Release();
        }
    }

    public byte[] Serialize(object APayload) => FSerializer.Serialize(APayload);

    public string IndexPath(string APersistenceId)
        => Path.Combine(FIndexDirectory, Hex(SHA256.HashData(Encoding.UTF8.GetBytes(APersistenceId))) + ".log");

    public static string Hex(byte[] ABytes) => Convert.ToHexString(ABytes).ToLowerInvariant();

    public static string Chain(string APrevious, long ASequenceNr, string APayloadHash)
        => Hex(SHA256.HashData(Encoding.ASCII.GetBytes(
            APrevious + "|" + ASequenceNr.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + APayloadHash)));

    public object LoadBlob(string AHash)
    {
        var path = Path.Combine(FObjects, AHash);
        var bytes = File.ReadAllBytes(path);

        if (Hex(SHA256.HashData(bytes)) != AHash)
        {
            throw new InvalidDataException("Payload blob hash mismatch: " + AHash + ".");
        }

        return FSerializer.Deserialize(bytes);
    }

    public void WriteBlob(string AHash, byte[] ABytes)
    {
        var path = Path.Combine(FObjects, AHash);

        if (File.Exists(path))
        {
            return;
        }

        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
        {
            stream.Write(ABytes);
            stream.Flush(true);
        }

        File.Move(temp, path, true);
    }

    public void AppendLine(string APersistenceId, string ALine)
    {
        using var stream = new FileStream(IndexPath(APersistenceId), FileMode.Append, FileAccess.Write);

        stream.Write(Encoding.ASCII.GetBytes(ALine + "\n"));
        stream.Flush(true);
    }

}
