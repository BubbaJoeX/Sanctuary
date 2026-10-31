using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The ball is lobbed: it lands at Target after FlightTime seconds.
/// </summary>
public class SoccerPacketHighArcBall : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketHighArcBall>
{
    public new const ushort OpCode = 18;

    public Vector4 Target;

    public byte Ball;

    public float FlightTime;

    public SoccerPacketHighArcBall() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Target);
        writer.Write(Ball);
        writer.Write(FlightTime);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketHighArcBall value)
    {
        value = new SoccerPacketHighArcBall();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Target))
            return false;

        if (!reader.TryRead(out value.Ball))
            return false;

        if (!reader.TryRead(out value.FlightTime))
            return false;

        return reader.RemainingLength == 0;
    }
}
