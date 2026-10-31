using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// An achievement with a character's progress, as the achievements window shows it: in SendSelf, Initialize and
/// Inspect. The client computes nothing: the bar, the counts and the times all come from the server.
/// </summary>
public class ClientAchievement : ISerializableType
{
    public int Id { get; set; }
    public int NameId { get; set; }
    public int DescriptionId { get; set; }

    public bool MembersOnly { get; set; }

    /// <summary>
    /// Unix seconds; 0 = not started.
    /// </summary>
    public ulong TimeStarted { get; set; }

    /// <summary>
    /// Unix seconds; 0 = not done. Done achievements are the ones whose trophy points count.
    /// </summary>
    public ulong TimeFinished { get; set; }

    /// <summary>
    /// The bar, 0 to 1.
    /// </summary>
    public float Progress { get; set; }

    public RewardBundleBase Reward { get; set; } = EmptyReward();

    /// <summary>
    /// Listed under their ObjectiveId: the objective packets find them by it.
    /// </summary>
    public List<QuestObjectiveInfo> Objectives { get; set; } = [];

    public int IconId { get; set; }
    public int IconTint { get; set; }

    /// <summary>
    /// An AchievementTrophies.txt row: the pop-up's colour, the trophy's name and points.
    /// </summary>
    public int TrophyId { get; set; }

    /// <summary>
    /// Added to the trophy's points.
    /// </summary>
    public int BonusPoints { get; set; }

    public bool SuppressToast { get; set; }

    /// <summary>
    /// An AchievementCategories.txt row: where the window lists it.
    /// </summary>
    public int CategoryId { get; set; }

    /// <summary>
    /// The bundle live sent with no reward (objectives always carry this one).
    /// </summary>
    public static RewardBundleBase EmptyReward() => new()
    {
        Success = false,
        Unknown3 = 0,
        Multiplier = 1.0f,
        IconId = -1,
        NameId = -1
    };

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(NameId);
        writer.Write(DescriptionId);
        writer.Write(MembersOnly);
        writer.Write(TimeStarted);
        writer.Write(TimeFinished);
        writer.Write(Progress);

        Reward.Serialize(writer);

        writer.Write(Objectives.Count);

        foreach (var objective in Objectives)
        {
            writer.Write(objective.ObjectiveId);
            objective.Serialize(writer);
        }

        writer.Write(IconId);
        writer.Write(IconTint);
        writer.Write(TrophyId);
        writer.Write(BonusPoints);

        // The client reads the flag before the category.
        writer.Write(SuppressToast);
        writer.Write(CategoryId);
    }
}
