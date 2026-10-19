namespace Symphact.Persistence;

/// <summary>
/// hu: Payload szerializáló a tartós tárakhoz (CAS). Az object payload-ot bájtokra és
/// vissza képezi; a típusnevet a bájtok hordozzák, így a visszaolvasás típushű.
/// <br />
/// en: Payload serializer for durable stores (CAS). Maps an object payload to bytes and
/// back; the bytes carry the type name so reading restores the original type.
/// </summary>
public interface IPayloadSerializer
{
    /// <summary>
    /// hu: A payload bájtokra képezése.
    /// <br />
    /// en: Serialise the payload to bytes.
    /// </summary>
    byte[] Serialize(object APayload);

    /// <summary>
    /// hu: A bájtok visszaképezése payload objektummá.
    /// <br />
    /// en: Deserialise the bytes back to a payload object.
    /// </summary>
    object Deserialize(byte[] ABytes);
}
