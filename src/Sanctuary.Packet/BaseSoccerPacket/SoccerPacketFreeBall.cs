using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A soccer player lets go of the ball (a kick).
/// </summary>
public class SoccerPacketFreeBall : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketFreeBall>
{
    public new const ushort OpCode = 12;

    public byte Ball;

    public Vector4 Position;

    public ulong Guid;

    public SoccerPacketFreeBall() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Ball);
        writer.Write(Position);
        writer.Write(Guid);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketFreeBall value)
    {
        value = new SoccerPacketFreeBall();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Ball))
            return false;

        if (!reader.TryRead(out value.Position))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        return reader.RemainingLength == 0;
    }
}
