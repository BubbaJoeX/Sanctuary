using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// One row of the client's group window (its GroupMemberDataSource: PlayerName, PlayerGuid, PlayerJobId,
/// PlayerJobIconId, PlayerJobBackgroundIconId, PlayerJobRank, IsChangingJob, InSameMinigame).
/// </summary>
public class GroupMember : ISerializableType
{
    public ulong Guid;
    public NameData Name = new();

    public int Unknown;

    public int ProfileId;

    public int Unknown2;

    public int ProfileIconId;
    public int ProfileBackgroundIconId;
    public int ProfileRank;

    public int Unknown3;

    public bool IsChangingJob;
    public bool Unknown4;

    /// <summary>
    /// Members with the same value show as in the same minigame. 0 outside one.
    /// </summary>
    public int MiniGameId;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Guid);
        Name.Serialize(writer);

        writer.Write(Unknown);
        writer.Write(ProfileId);
        writer.Write(Unknown2);
        writer.Write(ProfileIconId);
        writer.Write(ProfileBackgroundIconId);
        writer.Write(ProfileRank);
        writer.Write(Unknown3);

        writer.Write(IsChangingJob);
        writer.Write(Unknown4);

        writer.Write(MiniGameId);
    }
}
