using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The fisher clicking to reel in. Sent on to the players around them, it plays their reel animation (Reeling true)
/// or the slack one; for the fisher themselves it also makes the fish nosing the lure swim off.
/// </summary>
public class FishingPacketReelInRequest : BaseFishingPacket, ISerializablePacket, IDeserializable<FishingPacketReelInRequest>
{
    public new const ushort OpCode = 7;

    public ulong Guid;

    public bool Reeling;

    public FishingPacketReelInRequest() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Reeling);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out FishingPacketReelInRequest value)
    {
        value = new FishingPacketReelInRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Reeling))
            return false;

        return reader.RemainingLength == 0;
    }
}
