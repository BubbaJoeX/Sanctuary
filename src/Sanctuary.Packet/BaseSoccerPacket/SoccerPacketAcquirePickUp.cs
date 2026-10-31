using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A soccer player takes the power-up in Slot, for Seconds.
/// </summary>
public class SoccerPacketAcquirePickUp : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketAcquirePickUp>
{
    public new const ushort OpCode = 10;

    public byte Slot;

    public float Seconds;

    public ulong Guid;

    public SoccerPacketAcquirePickUp() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Slot);
        writer.Write(Seconds);
        writer.Write(Guid);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketAcquirePickUp value)
    {
        value = new SoccerPacketAcquirePickUp();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Slot))
            return false;

        if (!reader.TryRead(out value.Seconds))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        return reader.RemainingLength == 0;
    }
}
