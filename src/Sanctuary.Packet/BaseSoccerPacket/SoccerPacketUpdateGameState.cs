using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The match: the leader, the half, the score, each team's stats this half and the game state (1 starting, 7 ready, 8
/// half time, 10 the end, 11 kick-off, 12 playing, 16-18 a goal). The client sends one, state 9, when the half time
/// board closes.
/// </summary>
public class SoccerPacketUpdateGameState : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketUpdateGameState>
{
    public new const ushort OpCode = 5;

    /// <summary>
    /// -1 for none; at the end the local team "won" if it leads.
    /// </summary>
    public int LeadingTeam;

    /// <summary>
    /// 0 or 1.
    /// </summary>
    public int Period;

    /// <summary>
    /// Team 0's.
    /// </summary>
    public int Score;

    /// <summary>
    /// Team 1's.
    /// </summary>
    public int Score2;

    /// <summary>
    /// Team 0's; this half.
    /// </summary>
    public int Shots;

    public int Shots2;

    public int Knockdowns;

    public int Knockdowns2;

    public int Pickups;

    public int Pickups2;

    public int Steals;

    public int Steals2;

    public int State;

    /// <summary>
    /// Live 0, 1, then 8 once the players were placed.
    /// </summary>
    public int Unknown;

    public ulong Unknown2;

    /// <summary>
    /// Live 1 from the kick-off.
    /// </summary>
    public int Unknown3;

    /// <summary>
    /// Live -1, then 0 after a goal.
    /// </summary>
    public int Unknown4;

    public SoccerPacketUpdateGameState() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(LeadingTeam);
        writer.Write(Period);
        writer.Write(Score);
        writer.Write(Score2);
        writer.Write(Shots);
        writer.Write(Shots2);
        writer.Write(Knockdowns);
        writer.Write(Knockdowns2);
        writer.Write(Pickups);
        writer.Write(Pickups2);
        writer.Write(Steals);
        writer.Write(Steals2);
        writer.Write(State);
        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketUpdateGameState value)
    {
        value = new SoccerPacketUpdateGameState();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.LeadingTeam))
            return false;

        if (!reader.TryRead(out value.Period))
            return false;

        if (!reader.TryRead(out value.Score))
            return false;

        if (!reader.TryRead(out value.Score2))
            return false;

        if (!reader.TryRead(out value.Shots))
            return false;

        if (!reader.TryRead(out value.Shots2))
            return false;

        if (!reader.TryRead(out value.Knockdowns))
            return false;

        if (!reader.TryRead(out value.Knockdowns2))
            return false;

        if (!reader.TryRead(out value.Pickups))
            return false;

        if (!reader.TryRead(out value.Pickups2))
            return false;

        if (!reader.TryRead(out value.Steals))
            return false;

        if (!reader.TryRead(out value.Steals2))
            return false;

        if (!reader.TryRead(out value.State))
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
