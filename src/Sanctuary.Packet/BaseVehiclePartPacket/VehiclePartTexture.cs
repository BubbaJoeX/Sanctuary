using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A part's paint: a tint and a texture alias (the body's is the vehicle's own).
/// </summary>
public class VehiclePartTexture : ISerializableType
{
    public uint TintId;

    public string TextureAlias = string.Empty;

    public uint Unknown;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(TintId);
        writer.Write(TextureAlias);
        writer.Write(Unknown);
    }
}
