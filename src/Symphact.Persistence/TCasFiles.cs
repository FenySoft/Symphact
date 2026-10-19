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
    public const string Genesis = "0000000000000000000000000000000000000000000000000000000000000000";

    private readonly string FObjects;
    private readonly string FIndexDirectory;
    private readonly IPayloadSerializer FSerializer;

    public TCasFiles(string ARootDirectory, string AIndexDirectoryName, IPayloadSerializer? ASerializer)
    {
        ArgumentException.ThrowIfNullOrEmpty(ARootDirectory);

        FObjects = Path.Combine(ARootDirectory, "objects");
        FIndexDirectory = Path.Combine(ARootDirectory, AIndexDirectoryName);
        FSerializer = ASerializer ?? new TJsonPayloadSerializer();
        Directory.CreateDirectory(FObjects);
        Directory.CreateDirectory(FIndexDirectory);
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
