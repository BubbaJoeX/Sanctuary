namespace Sanctuary.Game.Achievements;

/// <summary>
/// The game events achievement objectives count. Events with a key match an objective's Keys; the rest are counters.
/// </summary>
public enum AchievementEvent
{
    None,

    /// <summary>
    /// A job's rank (key: the Profiles.json id; the count is the rank, not a sum).
    /// </summary>
    JobRank,

    /// <summary>
    /// A mob defeated (matched by its name).
    /// </summary>
    Kill,

    /// <summary>
    /// A battle won (key: the Battles.json id).
    /// </summary>
    Battle,

    /// <summary>
    /// A battle won by a full group (key: the Battles.json id).
    /// </summary>
    BattleFullGroup,

    /// <summary>
    /// A quest completed (key: the quest id).
    /// </summary>
    Quest,

    /// <summary>
    /// Any quest completed.
    /// </summary>
    QuestCount,

    /// <summary>
    /// A minigame finished, won or not (key: the minigame id).
    /// </summary>
    Minigame,

    /// <summary>
    /// A minigame won (key: the minigame id).
    /// </summary>
    MinigameWin,

    /// <summary>
    /// The items in one of the player's houses (the count is the house's total, not a sum).
    /// </summary>
    HouseItems,

    /// <summary>
    /// A visit to someone else's house.
    /// </summary>
    HouseVisit,

    /// <summary>
    /// A housing customization (a wallpaper, floor...) used.
    /// </summary>
    HouseCustomize,

    /// <summary>
    /// Coins earned through rewards (the count is the coins).
    /// </summary>
    RewardCoins,

    PetTrick,

    PetAcquired,

    /// <summary>
    /// A ride gained (key: the Mounts.json id).
    /// </summary>
    MountAcquired,

    StyleCard,

    /// <summary>
    /// A usable item used (a potion, food, scroll...).
    /// </summary>
    Consumable,

    /// <summary>
    /// A recipe learned (key: the Recipes.json id).
    /// </summary>
    Recipe,

    /// <summary>
    /// Another achievement finished (key: its id), for the "do all of these" ones.
    /// </summary>
    Achievement
}
