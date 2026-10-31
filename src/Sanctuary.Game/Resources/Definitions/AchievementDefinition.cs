using System.Collections.Generic;

using Sanctuary.Game.Achievements;

namespace Sanctuary.Game.Resources.Definitions;

/// <summary>
/// An achievement (see Achievements.json, the live catalogue): what the achievements window lists, the objectives
/// that complete it and the game events that count toward each.
/// </summary>
public sealed class AchievementDefinition
{
    public int Id { get; set; }

    public string Comment { get; set; } = string.Empty;

    public int NameId { get; set; }
    public int DescriptionId { get; set; }

    public int IconId { get; set; }
    public int IconTint { get; set; }

    /// <summary>
    /// An AchievementCategories.txt row (client-side): where the window lists it.
    /// </summary>
    public int CategoryId { get; set; }

    /// <summary>
    /// A trophy (Achievements.json Trophies, from the client's AchievementTrophies.txt): its points and effect.
    /// </summary>
    public int TrophyId { get; set; }

    public int BonusPoints { get; set; }

    public bool MembersOnly { get; set; }

    /// <summary>
    /// Given when it's done, or none.
    /// </summary>
    public AchievementRewardDefinition? Reward { get; set; }

    public List<AchievementObjectiveDefinition> Objectives { get; set; } = [];
}

public sealed class AchievementObjectiveDefinition
{
    /// <summary>
    /// Unique within the achievement; the client finds the objective by it.
    /// </summary>
    public int Id { get; set; }

    public string Comment { get; set; } = string.Empty;

    public int NameId { get; set; }
    public int DescriptionId { get; set; }

    /// <summary>
    /// The count that completes it (1 for a one-off).
    /// </summary>
    public int Required { get; set; } = 1;

    /// <summary>
    /// What counts toward it; none while the game has nothing that does (the card game, duels...).
    /// </summary>
    public AchievementTriggerDefinition? Trigger { get; set; }
}

/// <summary>
/// The game event that counts toward an objective, and which ones.
/// </summary>
public sealed class AchievementTriggerDefinition
{
    public AchievementEvent Event { get; set; }

    /// <summary>
    /// For events with a key (a job, battle, quest, minigame, recipe, ride or achievement id): the keys that count.
    /// Left out, any key counts; empty, none does yet (the battle or quest isn't in the game).
    /// </summary>
    public List<int>? Keys { get; set; }

    /// <summary>
    /// For kills: regular expressions matched against the mob's name. For other events, the names the keys stand
    /// for (documentation, to fill in Keys once those exist).
    /// </summary>
    public List<string>? Names { get; set; }

    /// <summary>
    /// For kills: the mobs' locale name ids, for mobs that have no name string.
    /// </summary>
    public List<int>? NameIds { get; set; }
}

public sealed class AchievementRewardDefinition
{
    public int ItemId { get; set; }
    public int Quantity { get; set; } = 1;

    public int NameId { get; set; }
    public int IconId { get; set; }
    public int IconTint { get; set; }

    /// <summary>
    /// The reward tooltip's text colour.
    /// </summary>
    public int TextColor { get; set; }

    public int DescriptionId { get; set; }
}

/// <summary>
/// An AchievementTrophies.txt row: how many points an achievement of this trophy is worth, and the effect played on
/// the player who earns it.
/// </summary>
public sealed class AchievementTrophyDefinition
{
    public int Id { get; set; }

    public string Comment { get; set; } = string.Empty;

    public int Points { get; set; }

    public int GainEffectId { get; set; }
}
