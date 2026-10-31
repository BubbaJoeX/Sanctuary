using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// One row of the lobby's queue list (78 bytes), in wire order. The live server's numbers line up with the encounter
/// each queue launches: <see cref="MatchType"/> is its MiniGameType, <see cref="Param7"/> its kind and
/// <see cref="Unknown48"/> its job type.
/// </summary>
public class MatchmakingQueueDefinition : ISerializableType
{
    /// <summary>
    /// The key for joining the queue and for its requests.
    /// </summary>
    public int QueueId;

    /// <summary>
    /// The row's title (a locale id).
    /// </summary>
    public int NameId;

    public int MatchType;

    public int MinPlayers;

    /// <summary>
    /// Also the largest group that can queue: the lobby turns a bigger one away.
    /// </summary>
    public int MaxPlayers;

    public int MinTeams;
    public int MaxTeams;

    public int MaxGameStartDelay;

    /// <summary>
    /// Copied into the client's AddMatchRequest.
    /// </summary>
    public int Param1;

    public int Param2;
    public int Param3;
    public int Param4;
    public int Param5;
    public int Param6;
    public int Param7;

    /// <summary>
    /// The Quick Play page's text (a locale id).
    /// </summary>
    public int EncounterDescriptionId;

    /// <summary>
    /// The Quick Play page's picture (an Images.txt id, a start-panel graphic).
    /// </summary>
    public int EncounterIconId;

    public int Unknown48;
    public int Unknown4C;

    public bool MemberOnly;

    public bool Unknown51;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(QueueId);
        writer.Write(NameId);
        writer.Write(MatchType);
        writer.Write(MinPlayers);
        writer.Write(MaxPlayers);
        writer.Write(MinTeams);
        writer.Write(MaxTeams);
        writer.Write(MaxGameStartDelay);
        writer.Write(Param1);
        writer.Write(Param2);
        writer.Write(Param3);
        writer.Write(Param4);
        writer.Write(Param5);
        writer.Write(Param6);
        writer.Write(Param7);
        writer.Write(EncounterDescriptionId);
        writer.Write(EncounterIconId);
        writer.Write(Unknown48);
        writer.Write(Unknown4C);
        writer.Write(MemberOnly);
        writer.Write(Unknown51);
    }
}
