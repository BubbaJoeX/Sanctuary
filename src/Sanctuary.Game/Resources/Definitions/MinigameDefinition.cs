using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

/// <summary>
/// A minigame the Games window offers (Resources/Minigames.json). The id is the game's MiniGameData.txt id,
/// which is also its activity id and the stage id its packets carry.
/// </summary>
public class MinigameDefinition
{
    public int Id { get; set; }

    /// <summary>
    /// Its English name, for logs and chat (the locale has the one the client shows).
    /// </summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Which server runs it: the <see cref="Minigames.FlashMinigameAttribute"/> name of a
    /// <see cref="Minigames.FlashMinigame"/> ("Wheel", "MatchThree", "Cooking", ...).
    /// </summary>
    public string Game { get; set; } = string.Empty;

    /// <summary>
    /// The movie MiniGameFlash loads ("game_mining.swf"; the client fetches the .gfx).
    /// </summary>
    public string Swf { get; set; } = string.Empty;

    public int NameId { get; set; }
    public int DescriptionId { get; set; }
    public int IconId { get; set; }
    public int Difficulty { get; set; } = 1;

    /// <summary>
    /// ActivityCategories.txt id: the Games window tile it's listed under. 0 keeps it out of the window
    /// (started from the world or a quest only).
    /// </summary>
    public int Category { get; set; }

    public string DetailImage { get; set; } = string.Empty;
    public string ThumbnailImage { get; set; } = string.Empty;

    /// <summary>
    /// Which of its game's level sets, recipes or boards this one plays; the game decides what it means.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Coins for a win.
    /// </summary>
    public int Coins { get; set; }

    /// <summary>
    /// Stars for a win, to <see cref="ProfileId"/> when the player has that job, else to their active one.
    /// </summary>
    public int Stars { get; set; }

    /// <summary>
    /// The job the game trains (Profiles.json id: 4 Postman, 14 Miner, 16 Blacksmith, 45 Chef), or 0.
    /// </summary>
    public int ProfileId { get; set; }

    /// <summary>
    /// Items for a win: what was mined, forged, smelted or cooked.
    /// </summary>
    public List<MinigameItemReward> Items { get; set; } = [];

    /// <summary>
    /// A Rewards.json table rolled once on a win, or empty.
    /// </summary>
    public string RewardTable { get; set; } = string.Empty;

    /// <summary>
    /// Shares of the coins and stars a loss still pays, from 0 to 1.
    /// </summary>
    public float LossShare { get; set; } = 0.25f;
}

public class MinigameItemReward
{
    public int ItemId { get; set; }
    public int Quantity { get; set; } = 1;
}
