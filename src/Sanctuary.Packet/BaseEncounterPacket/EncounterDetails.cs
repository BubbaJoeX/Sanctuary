using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// What the start panel and the HUD show of a battle, and the rules the client keeps for it (the time limit and
/// the respawn countdown).
/// </summary>
public class EncounterDetails : ISerializableType
{
    public const int BattleKind = 1;

    /// <summary>
    /// The battle instance (the header's EncounterId).
    /// </summary>
    public int Id;

    /// <summary>
    /// Not read by the client (live: 2 at the offer, 4 once in).
    /// </summary>
    public int Unknown = 2;

    public List<List<EncounterParticipant>> Teams = [];

    /// <summary>
    /// Six ints each; live sent none.
    /// </summary>
    public List<int[]> TeamData = [];

    /// <summary>
    /// 1 battle, 2 kart race, 3 demolition derby, 4 soccer; 7 starts without a panel.
    /// </summary>
    public int Kind = BattleKind;

    public int Unknown2;

    /// <summary>
    /// A battle must show it: without it the client asks to start a minigame instead.
    /// </summary>
    public bool ShowStartPanel = true;

    public bool Unknown3 = true;

    /// <summary>
    /// Holds the client's queued popups back while set.
    /// </summary>
    public bool HoldPopups;

    /// <summary>
    /// Seconds; the client counts the last two minutes down on screen.
    /// </summary>
    public int TimeLimitSeconds;

    /// <summary>
    /// The knocked out window's countdown when it doesn't give its own.
    /// </summary>
    public int RespawnMilliseconds;

    public MiniGameInfoPacket Info = new();

    public bool Unknown4;

    /// <summary>
    /// Without it the client ignores the packet.
    /// </summary>
    public bool Gate = true;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(Unknown);

        writer.Write(Teams.Count);

        foreach (var team in Teams)
        {
            writer.Write(team.Count);

            foreach (var participant in team)
                participant.Serialize(writer);
        }

        writer.Write(TeamData.Count);

        foreach (var data in TeamData)
        {
            for (var i = 0; i < 6; i++)
                writer.Write(i < data.Length ? data[i] : 0);
        }

        writer.Write(Kind);
        writer.Write(Unknown2);
        writer.Write(ShowStartPanel);
        writer.Write(Unknown3);
        writer.Write(HoldPopups);
        writer.Write(TimeLimitSeconds);
        writer.Write(RespawnMilliseconds);

        Info.WriteBody(writer);

        writer.Write(Unknown4);
        writer.Write(Gate);
    }
}
