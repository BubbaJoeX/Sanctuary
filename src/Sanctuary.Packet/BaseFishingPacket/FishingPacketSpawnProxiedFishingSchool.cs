using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// A school of fish on the pond's surface, which the Fish Finder points at. The client ignores a school id it
/// already has.
/// </summary>
public class FishingPacketSpawnProxiedFishingSchool : BaseFishingPacket, ISerializablePacket
{
    public new const ushort OpCode = 11;

    public int SchoolId;

    public Vector4 Position;

    /// <summary>
    /// Unused by the client.
    /// </summary>
    public Vector4 Unknown;

    public List<FishingSchoolModel> Models = [];

    /// <summary>
    /// Played on the school's first model.
    /// </summary>
    public List<int> CompositeEffectIds = [];

    /// <summary>
    /// Unused by the client.
    /// </summary>
    public ulong Unknown2;

    public FishingPacketSpawnProxiedFishingSchool() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(SchoolId);
        writer.Write(Position);
        writer.Write(Unknown);
        writer.Write(Models);
        writer.Write(CompositeEffectIds);
        writer.Write(Unknown2);

        return writer.Buffer;
    }
}
