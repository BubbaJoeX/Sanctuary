using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A soccer player's movement and kick tuning (run speed first), sent once per player at the start. The 168 bytes
/// after the guid are 31 floats, then a u32, four floats, a u32, four floats and a u32, kept as the live game's.
/// </summary>
public class SoccerPacketSetSoccerPlayerOneTimeStats : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketSetSoccerPlayerOneTimeStats>
{
    public new const ushort OpCode = 21;

    public const int StatsLength = 168;

    public ulong Guid;

    public byte[] Stats = new byte[StatsLength];

    public SoccerPacketSetSoccerPlayerOneTimeStats() : base(OpCode)
    {
    }

    /// <summary>
    /// The first float: the player's run speed.
    /// </summary>
    public float RunSpeed => BitConverter.ToSingle(Stats, 0);

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Stats);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketSetSoccerPlayerOneTimeStats value)
    {
        value = new SoccerPacketSetSoccerPlayerOneTimeStats();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader) || !reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryReadExact(StatsLength, out var stats))
            return false;

        value.Stats = stats.ToArray();

        return reader.RemainingLength == 0;
    }
}
