using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A soccer player takes the ball (from PreviousHolder, on a steal).
/// </summary>
public class SoccerPacketAcquireBall : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketAcquireBall>
{
    public new const ushort OpCode = 11;

    public byte Ball;

    public ulong Guid;

    public ulong PreviousHolder;

    public SoccerPacketAcquireBall() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Ball);
        writer.Write(Guid);
        writer.Write(PreviousHolder);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketAcquireBall value)
    {
        value = new SoccerPacketAcquireBall();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Ball))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.PreviousHolder))
            return false;

        return reader.RemainingLength == 0;
    }
}
