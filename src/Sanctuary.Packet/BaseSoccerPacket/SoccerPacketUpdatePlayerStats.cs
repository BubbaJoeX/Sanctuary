using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A soccer player's tallies and points, for the scoreboard.
/// </summary>
public class SoccerPacketUpdatePlayerStats : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketUpdatePlayerStats>
{
    public new const ushort OpCode = 15;

    public ulong Guid;

    public int Knockdowns;

    public int Steals;

    public int Dodges;

    public int Goals;

    public int Score;

    public SoccerPacketUpdatePlayerStats() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Knockdowns);
        writer.Write(Steals);
        writer.Write(Dodges);
        writer.Write(Goals);
        writer.Write(Score);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketUpdatePlayerStats value)
    {
        value = new SoccerPacketUpdatePlayerStats();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Knockdowns))
            return false;

        if (!reader.TryRead(out value.Steals))
            return false;

        if (!reader.TryRead(out value.Dodges))
            return false;

        if (!reader.TryRead(out value.Goals))
            return false;

        if (!reader.TryRead(out value.Score))
            return false;

        return reader.RemainingLength == 0;
    }
}
