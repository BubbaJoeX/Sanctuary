using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The match clock: what is left of the half (the timer) and the seconds since the match began.
/// </summary>
public class SoccerPacketUpdateGameTime : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketUpdateGameTime>
{
    public new const ushort OpCode = 23;

    public float PeriodLeft;

    public float Clock;

    public SoccerPacketUpdateGameTime() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PeriodLeft);
        writer.Write(Clock);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketUpdateGameTime value)
    {
        value = new SoccerPacketUpdateGameTime();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.PeriodLeft))
            return false;

        if (!reader.TryRead(out value.Clock))
            return false;

        return reader.RemainingLength == 0;
    }
}
