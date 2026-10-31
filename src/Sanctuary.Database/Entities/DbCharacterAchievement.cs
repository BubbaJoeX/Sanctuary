using System.Collections.Generic;

namespace Sanctuary.Database.Entities;

/// <summary>
/// An achievement a character has started (and maybe finished). Achievements without a row aren't started.
/// </summary>
public class DbCharacterAchievement
{
    public int AchievementId { get; set; }

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;

    /// <summary>
    /// Unix seconds.
    /// </summary>
    public long Started { get; set; }

    /// <summary>
    /// Unix seconds; null while it isn't done.
    /// </summary>
    public long? Finished { get; set; }

    public ICollection<DbCharacterAchievementObjective> Objectives { get; set; } = new HashSet<DbCharacterAchievementObjective>();
}
