using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Moves a school. The client snaps it there, so schools are moved a little every tick.
/// </summary>
public class FishingPacketUpdateProxiedFishingSchool : BaseFishingPacket, ISerializablePacket
{
    public new const ushort OpCode = 13;

    public int SchoolId;

    public Vector4 Position;

    public FishingPacketUpdateProxiedFishingSchool() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(SchoolId);
        writer.Write(Position);

        return writer.Buffer;
    }
}
