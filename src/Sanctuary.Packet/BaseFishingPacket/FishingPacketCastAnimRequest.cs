using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The fisher starting their cast. The client sends it once per cast; sent on to the players around them, it plays
/// their cast animation (longer casts swing harder).
/// </summary>
public class FishingPacketCastAnimRequest : BaseFishingPacket, ISerializablePacket, IDeserializable<FishingPacketCastAnimRequest>
{
    public new const ushort OpCode = 5;

    public ulong Guid;

    /// <summary>
    /// How far the cast goes, across the ground.
    /// </summary>
    public float Distance;

    /// <summary>
    /// From the fisher to where they aimed, flat (y is 0) and not normalized.
    /// </summary>
    public Vector4 Direction;

    public FishingPacketCastAnimRequest() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Distance);
        writer.Write(Direction);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out FishingPacketCastAnimRequest value)
    {
        value = new FishingPacketCastAnimRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Distance))
            return false;

        if (!reader.TryRead(out value.Direction))
            return false;

        return reader.RemainingLength == 0;
    }
}
