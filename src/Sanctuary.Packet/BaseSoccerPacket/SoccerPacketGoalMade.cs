using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A goal: plays its effect at the goal hit, and the scoring team picks the banner.
/// </summary>
public class SoccerPacketGoalMade : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketGoalMade>
{
    public new const ushort OpCode = 2;

    public uint Ball;

    /// <summary>
    /// 0 the goal at the low x end, 1 the high.
    /// </summary>
    public uint Goal;

    public uint ScoringTeam;

    public SoccerPacketGoalMade() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Ball);
        writer.Write(Goal);
        writer.Write(ScoringTeam);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketGoalMade value)
    {
        value = new SoccerPacketGoalMade();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Ball))
            return false;

        if (!reader.TryRead(out value.Goal))
            return false;

        if (!reader.TryRead(out value.ScoringTeam))
            return false;

        return reader.RemainingLength == 0;
    }
}
