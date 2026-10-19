using System.Text.Json;

namespace Symphact.Persistence;

/// <summary>
/// hu: System.Text.Json (BCL) alapú payload szerializáló. A borítékban a típus
/// assembly-qualified neve utazik, így a visszaolvasás az eredeti típust adja.
/// <br />
/// en: System.Text.Json (BCL) based payload serializer. The envelope carries the
/// assembly-qualified type name, so reading restores the original type.
/// </summary>
public sealed class TJsonPayloadSerializer : IPayloadSerializer
{
    private sealed record TEnvelope(string Type, JsonElement Value);

    /// <inheritdoc />
    public byte[] Serialize(object APayload)
    {
        ArgumentNullException.ThrowIfNull(APayload);

        var type = APayload.GetType();
        var envelope = new TEnvelope(
            type.AssemblyQualifiedName ?? throw new InvalidOperationException(type.ToString()),
            JsonSerializer.SerializeToElement(APayload, type));

        return JsonSerializer.SerializeToUtf8Bytes(envelope);
    }

    /// <inheritdoc />
    public object Deserialize(byte[] ABytes)
    {
        var envelope = JsonSerializer.Deserialize<TEnvelope>(ABytes)
            ?? throw new InvalidDataException("Empty payload envelope.");
        var type = Type.GetType(envelope.Type, throwOnError: true)!;

        return envelope.Value.Deserialize(type)
            ?? throw new InvalidDataException("Payload deserialised to null.");
    }
}
