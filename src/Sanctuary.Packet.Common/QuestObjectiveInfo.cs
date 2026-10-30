using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// An objective: a quest's goal, or one of a minigame's or battle's (its Info lists them, and the minigame HUD toasts
/// and counts them). The quest log doesn't read IsBonus, Kind, MembersOnly or Category; the minigame HUD does.
/// </summary>
public class QuestObjectiveInfo : ISerializableType
{
    public int ObjectiveId { get; set; }
    public int NameId { get; set; }
    public int DescriptionId { get; set; }

    /// <summary>
    /// A minigame's bonus goal: no toasts, and it counts as bonus on the end screen.
    /// </summary>
    public bool IsBonus { get; set; }

    public RewardBundleBase RewardBundle { get; set; } = new()
    {
        Success = false,
        Unknown3 = 0,
        Multiplier = 0f
    };

    // 0 = inactive, 1 = active, 2 = complete, 3 = failed.
    public int State { get; set; }
    public int CurrentCount { get; set; }
    public int RequiredCount { get; set; }

    /// <summary>
    /// 1 for a failure condition ("don't get knocked out 5 times").
    /// </summary>
    public int Kind { get; set; }

    public bool MembersOnly { get; set; }

    /// <summary>
    /// The text's prefix: 1 primary, 2 secondary, 3 job, 4 bonus, 5 elite, 0 none.
    /// </summary>
    public int Category { get; set; }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(ObjectiveId);
        writer.Write(NameId);
        writer.Write(DescriptionId);
        writer.Write(IsBonus);

        RewardBundle.Serialize(writer);

        writer.Write(State);
        writer.Write(CurrentCount);
        writer.Write(RequiredCount);
        writer.Write(Kind);
        writer.Write(MembersOnly);
        writer.Write(Category);
    }
}
