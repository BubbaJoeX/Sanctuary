namespace Sanctuary.Game.Minigames.MatchThree;

/// <summary>
/// A kind of tile a level spawns (a MatchThreeSpawnData.txt row, the movies' SpawnData): its category and type,
/// how often it comes up against the others of its category, and its quota (the bin the movie shows for it).
/// </summary>
public sealed class MatchThreeSpawnData(int id)
{
    public const int CategoryCollectable = 0;
    public const int CategoryBonus = 1;
    public const int CategorySelectable = 2;
    public const int CategoryPowerup = 3;
    public const int CategoryLootDrop = 4;

    public int Id { get; } = id;

    public int Category { get; set; }

    /// <summary>
    /// The type within its category: the value is the category's base plus this (a powerup's is 0-53).
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// The tile value, set when a level takes the row.
    /// </summary>
    public int Value { get; set; }

    public int SpawnChance { get; set; }

    /// <summary>
    /// Tiles to collect for the level to succeed; 0 for none.
    /// </summary>
    public int Quota { get; set; }

    /// <summary>
    /// Tiles per point on the bin's counter. A selectable or collectable with no quota and 0 here gets no bin.
    /// </summary>
    public int RequiredPerReward { get; set; } = 1;

    /// <summary>
    /// The most of this kind a level creates (0 for no limit); past it, it stops spawning.
    /// </summary>
    public int MaximumToCreate { get; set; }

    /// <summary>
    /// The most of this kind that count when collected (0 for no limit).
    /// </summary>
    public int MaximumToCollect { get; set; }

    public int RewardSetId { get; set; }

    /// <summary>
    /// The bin's icon (the movie keeps it; it draws the tile's own art).
    /// </summary>
    public int IconId { get; set; }

    /// <summary>
    /// The bin the movie shows it in, set when the level starts.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// A powerup's seconds: the time freeze adds them, the score multipliers last them.
    /// </summary>
    public int Duration { get; set; }

    /// <summary>
    /// A pressed powerup's strength: the tiles dynamite blasts, wild tiles makes, destruct clears or shuffle swaps.
    /// </summary>
    public int Power { get; set; }

    public int CollectionType { get; set; }

    public int TimerId { get; set; }

    public bool WithinCreateLimit(int count) => MaximumToCreate < 1 || count < MaximumToCreate;

    public int LimitToCollectLimit(int count) => MaximumToCollect < 1 ? count : System.Math.Min(count, MaximumToCollect);
}

/// <summary>
/// A MatchThreeTimerData.txt row (the movies' TimerData): how a chain would add time. The movie's server loads the
/// table but its levels never read it (a chain adds two seconds a tile), so it's kept only as the data says.
/// </summary>
public sealed record MatchThreeTimerData(int Id, int Type, double PerChainM, double PerChainF, double PerChainB, int PerTile)
{
    public const int NoChain = 0;
    public const int Log = 1;
    public const int Linear = 2;

    public double GetChain(int chain)
    {
        return Type switch
        {
            Log => PerChainM * System.Math.Log(PerChainF * chain) + PerChainB,
            Linear => PerChainM * chain + PerChainB,
            _ => 0
        };
    }
}
