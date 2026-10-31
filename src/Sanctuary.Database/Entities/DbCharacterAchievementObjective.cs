namespace Sanctuary.Database.Entities;

/// <summary>
/// How far a character is with one objective of a started achievement.
/// </summary>
public class DbCharacterAchievementObjective
{
    public int AchievementId { get; set; }

    public ulong CharacterId { get; set; }
    public DbCharacterAchievement Achievement { get; set; } = null!;

    public int ObjectiveId { get; set; }

    public int Count { get; set; }

    public bool Completed { get; set; }
}
