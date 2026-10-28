namespace Sanctuary.Game.Minigames.MatchThree;

/// <summary>
/// Tile values (the movies' GameData). A tile's value is its category, the hundreds, plus its type: 403 is the
/// third selectable (the movie shows "SelectableTile3"), 613 the 14th collectable. The negative values only ask the
/// tile generator for "any" or "a regular" tile; grid templates use 0-4 for the build kinds instead.
/// </summary>
public static class MatchThreeTiles
{
    public const int BuildRegular = -4;
    public const int BuildAny = -3;
    public const int Regular = -2;
    public const int Any = -1;

    public const int Empty = 0;
    public const int DropZone = 100;

    /// <summary>
    /// Tiles from here up are drawn (and can be picked at random by the powerups).
    /// </summary>
    public const int Visible = 200;

    public const int Wildcard = 200;
    public const int Hazard = 300;
    public const int HazardLock = 301;

    public const int Selectable = 400;
    public const int SelectableEnd = 499;

    /// <summary>
    /// A locked selectable goes to the movie as its value plus this (the movie's TILE_LOCKED, 500).
    /// </summary>
    public const int Locked = 500;
    public const int LockDelta = Locked - Selectable;

    public const int FallTypeStart = 600;

    public const int Collectable = 600;
    public const int CollectableEnd = 699;

    public const int Bonus = 700;
    public const int BonusEnd = 799;

    public const int Blocker = 800;

    public const int Powerup = 900;
    public const int PowerupFreeze = 900;
    public const int PowerupScore2X = 901;
    public const int PowerupScore5X = 902;

    /// <summary>
    /// Tiles from <see cref="FallTypeStart"/> up to here (exclusive) fall off the bottom into a drop zone.
    /// </summary>
    public const int FallTypeEnd = 949;

    /// <summary>
    /// Powerups from here up are pressed rather than collected (the movie's TILE_POWERUP_PRESSME).
    /// </summary>
    public const int PowerupPressMe = 950;
    public const int PowerupDynamite = 950;
    public const int PowerupWildTile = 951;
    public const int PowerupDestruct = 952;
    public const int PowerupShuffle = 953;

    public const int TransformTypeStart = 999;
    public const int TransformTypeEnd = 1000;

    public const int LootDrop = 1000;
    public const int LootDropCommon = 1000;
    public const int LootDropSpecial = 1001;
    public const int LootDropRare = 1002;

    public const int ChainScore = 400;
    public const int ChainBonusTileScore = 200;
    public const int CollectableScore = 500;
    public const int BonusScore = 2000;
    public const int PowerupScore = 250;

    public const int MinimumChainLength = 3;
    public const int MinimumChainTimeBonus = 1;

    /// <summary>
    /// Why the board was rebuilt (the movie's RESHUFFLE_NOMOVES).
    /// </summary>
    public const int ReshuffleNoMoves = 1;

    /// <summary>
    /// Why the timer or score moved, for the movie's feedback text (TIMER_* and SCORE_*).
    /// </summary>
    public const int TimerChain = 1;
    public const int ScoreChain = 0;
    public const int ScoreBonus = 1;
    public const int ScorePowerup = 2;

    /// <summary>
    /// The hundreds of a value: its category.
    /// </summary>
    public static int CategoryOf(int value) => (int)System.Math.Floor(value / 100.0) * 100;
}

/// <summary>
/// The level flags of MatchThreeLevelData.txt's TYPE column (the movies' M3LEVEL_*).
/// </summary>
[System.Flags]
public enum MatchThreeLevelType
{
    None = 0,
    Drag = 1,

    /// <summary>
    /// The level ends as soon as every quota is met, not only when its time runs out.
    /// </summary>
    StopAtQuota = 2,
    Meter = 4,
    StopGame = 8,

    /// <summary>
    /// The movie announces it as a bonus level.
    /// </summary>
    BonusLevel = 16
}

/// <summary>
/// The OnLevelEventMsg types (the movies' GameNetworkEvent), both ways.
/// </summary>
public static class MatchThreeEvents
{
    public const int LevelReshuffle = 11;
    public const int LevelNextWave = 12;
    public const int ScoreValue = 20;
    public const int ScoreIncrement = 21;
    public const int ScoreMultiplier = 22;
    public const int ScoreMultiplierExpired = 23;
    public const int TimerIncrement = 25;
    public const int MeterUpdate = 30;
    public const int MeterInit = 32;
    public const int PowerupFreeze = 40;

    // From the movie.
    public const int ClientFall = 50;
    public const int ClientConvert = 51;
    public const int ClientTileQueueAccept = 60;
    public const int ClientTileQueueReject = 61;
}
