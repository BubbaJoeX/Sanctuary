using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A soccer player's special animation time.
/// </summary>
public class SoccerPacketSpecialAnimTime : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketSpecialAnimTime>
{
    public new const ushort OpCode = 22;

    public ulong Guid;

    public float Time;

    public SoccerPacketSpecialAnimTime() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Time);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketSpecialAnimTime value)
    {
        value = new SoccerPacketSpecialAnimTime();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Time))
            return false;

        return reader.RemainingLength == 0;
    }
}
