using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A ball sound: 1 a goalie's block, anything else a bounce.
/// </summary>
public class SoccerPacketBallBounce : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketBallBounce>
{
    public new const ushort OpCode = 25;

    public uint Ball;

    public uint Kind;

    public SoccerPacketBallBounce() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Ball);
        writer.Write(Kind);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketBallBounce value)
    {
        value = new SoccerPacketBallBounce();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Ball))
            return false;

        if (!reader.TryRead(out value.Kind))
            return false;

        return reader.RemainingLength == 0;
    }
}
