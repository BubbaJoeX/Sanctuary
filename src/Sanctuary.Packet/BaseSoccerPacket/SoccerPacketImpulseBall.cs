using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A kick's push on the ball.
/// </summary>
public class SoccerPacketImpulseBall : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketImpulseBall>
{
    public new const ushort OpCode = 13;

    public uint Ball;

    public Vector4 Impulse;

    public float Unknown;

    public float Unknown2;

    public uint KickType;

    public float Unknown3;

    public SoccerPacketImpulseBall() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Ball);
        writer.Write(Impulse);
        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(KickType);
        writer.Write(Unknown3);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketImpulseBall value)
    {
        value = new SoccerPacketImpulseBall();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Ball))
            return false;

        if (!reader.TryRead(out value.Impulse))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        if (!reader.TryRead(out value.Unknown2))
            return false;

        if (!reader.TryRead(out value.KickType))
            return false;

        if (!reader.TryRead(out value.Unknown3))
            return false;

        return reader.RemainingLength == 0;
    }
}
