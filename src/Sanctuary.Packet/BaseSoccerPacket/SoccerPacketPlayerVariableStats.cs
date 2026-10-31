using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A soccer player's stats (kick power, footwork, speed, toughness, tackle power) and what power-ups changed.
/// </summary>
public class SoccerPacketPlayerVariableStats : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketPlayerVariableStats>
{
    public new const ushort OpCode = 19;

    public ulong Guid;

    public int Stat;

    public int Stat2;

    public int Stat3;

    public int Stat4;

    public int Stat5;

    public float Unknown;

    /// <summary>
    /// Live 100 with a Toughness Charm.
    /// </summary>
    public int Unknown2;

    /// <summary>
    /// Live -0.75 with a Quick Charge.
    /// </summary>
    public float Unknown3;

    public float Unknown4;

    public SoccerPacketPlayerVariableStats() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Stat);
        writer.Write(Stat2);
        writer.Write(Stat3);
        writer.Write(Stat4);
        writer.Write(Stat5);
        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketPlayerVariableStats value)
    {
        value = new SoccerPacketPlayerVariableStats();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Stat))
            return false;

        if (!reader.TryRead(out value.Stat2))
            return false;

        if (!reader.TryRead(out value.Stat3))
            return false;

        if (!reader.TryRead(out value.Stat4))
            return false;

        if (!reader.TryRead(out value.Stat5))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        if (!reader.TryRead(out value.Unknown2))
            return false;

        if (!reader.TryRead(out value.Unknown3))
            return false;

        if (!reader.TryRead(out value.Unknown4))
            return false;

        return reader.RemainingLength == 0;
    }
}
