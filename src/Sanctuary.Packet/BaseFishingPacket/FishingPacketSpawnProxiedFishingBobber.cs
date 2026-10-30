using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Puts a fisher's bobber on the water. For the fisher it ends the cast (the client goes underwater 2 seconds
/// later); for anyone else it shows them fishing. The fisher must already be visible to the client it's sent to.
/// </summary>
public class FishingPacketSpawnProxiedFishingBobber : BaseFishingPacket, ISerializablePacket
{
    public new const ushort OpCode = 8;

    public ulong Guid;

    /// <summary>
    /// A Models.txt bobber (a fishing rod's Param1). 0 for none; an unknown id crashes the client.
    /// </summary>
    public int ModelId;

    public Vector4 Position;

    /// <summary>
    /// Which way the bobber faces: flat, from the fisher out to it.
    /// </summary>
    public Vector4 Forward;

    public FishingPacketSpawnProxiedFishingBobber() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(ModelId);
        writer.Write(Position);
        writer.Write(Forward);

        return writer.Buffer;
    }
}
