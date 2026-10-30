using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

/// <summary>
/// Fishing.json: the fish, what else a line can bring up, the rods and the six fishing ponds. The original
/// servers' fishing configs are lost; the fish, their sizes and their favorite lures come from the client's
/// items and locale, the ponds from their zones' terrain.
/// </summary>
public sealed class FishingDefinition
{
    /// <summary>
    /// Given and equipped when a Fisherman reaches a pond with no rod: the client won't cast without one.
    /// </summary>
    public int StarterRodItemId { get; set; }

    /// <summary>
    /// The Npcs.json fisherman standing by each pond's arrival spot, who sells rods and lures and takes
    /// players back.
    /// </summary>
    public int GuideNpcId { get; set; }

    public List<FishingRodDefinition> Rods { get; set; } = [];

    public List<FishDefinition> Fish { get; set; } = [];

    public List<FishingJunkDefinition> Junk { get; set; } = [];

    public List<FishingTreasureDefinition> Treasure { get; set; } = [];

    public List<FishingPondDefinition> Ponds { get; set; } = [];
}

/// <summary>
/// A fishing rod, known by the bobber model its item names in Param1.
/// </summary>
public sealed class FishingRodDefinition
{
    public string? Comment { get; set; }

    public int BobberModelId { get; set; }

    /// <summary>
    /// The one of its tints the pond guide sells.
    /// </summary>
    public int ItemId { get; set; }

    public float MaxCastDistance { get; set; }

    /// <summary>
    /// 0 for the starter rod; each tier lands bigger and rarer fish a little more often and loses fewer.
    /// </summary>
    public int Tier { get; set; }
}

public enum FishSize
{
    Small = 1,
    Medium = 2,
    Large = 3,
    ExtraLarge = 4
}

public enum FishingRegion
{
    Snowhill,
    SacredGrove,
    Briarwood
}

public sealed class FishDefinition
{
    /// <summary>
    /// The Fish Finder's FishType.
    /// </summary>
    public int Id { get; set; }

    public string? Comment { get; set; }

    public int NameId { get; set; }

    public int IconId { get; set; }

    /// <summary>
    /// Null for a fish found in every pond.
    /// </summary>
    public FishingRegion? Region { get; set; }

    /// <summary>
    /// The lure (an item) that makes this fish bite more often; 0 when no lure tempts it.
    /// </summary>
    public int LureItemId { get; set; }

    /// <summary>
    /// 1 (common) to 4 (legendary).
    /// </summary>
    public int Rarity { get; set; }

    /// <summary>
    /// How it looks underwater: 1670 thin, 1671 medium or 1672 fat.
    /// </summary>
    public int ModelId { get; set; }

    /// <summary>
    /// The item caught in each size the fish comes in.
    /// </summary>
    public Dictionary<FishSize, int> Items { get; set; } = [];
}

/// <summary>
/// Something other than a fish on the hook: an old boot, soggy socks.
/// </summary>
public sealed class FishingJunkDefinition
{
    public string? Comment { get; set; }

    public int ItemId { get; set; }
}

/// <summary>
/// A coin pouch or a treasure chest on the hook, paid out from a Rewards.json table.
/// </summary>
public sealed class FishingTreasureDefinition
{
    public string? Comment { get; set; }

    public int NameId { get; set; }

    public int IconId { get; set; }

    public FishSize Size { get; set; }

    public required string RewardTable { get; set; }

    public float Weight { get; set; }
}

public sealed class FishingPondDefinition
{
    public string? Comment { get; set; }

    /// <summary>
    /// The pond's world zone (Zones/*.json).
    /// </summary>
    public required string Zone { get; set; }

    public int ZoneId { get; set; }

    public FishingRegion Region { get; set; }

    /// <summary>
    /// The Npcs.json fishing hole in Fabled Realms that leads here.
    /// </summary>
    public int HoleNpcId { get; set; }

    /// <summary>
    /// The pond's surface. Casts must land on it.
    /// </summary>
    public float WaterHeight { get; set; }

    public required string UnderwaterSky { get; set; }

    /// <summary>
    /// Where players arrive, on the shore facing the water.
    /// </summary>
    public required FishingSpotDefinition Arrival { get; set; }

    public required FishingSpotDefinition Guide { get; set; }

    /// <summary>
    /// Where the guide sends players back to, beside the fishing hole.
    /// </summary>
    public required FishingSpotDefinition Return { get; set; }

    public int ReturnZoneId { get; set; }

    /// <summary>
    /// Every fish that bites here.
    /// </summary>
    public List<int> Fish { get; set; } = [];

    public List<FishingSchoolDefinition> Schools { get; set; } = [];
}

public sealed class FishingSpotDefinition
{
    public float[] Position { get; set; } = [];

    public float Heading { get; set; }
}

/// <summary>
/// A school on the pond's surface. A cast that lands on it hooks its fish far more often, and the Fish
/// Finder points at it for them.
/// </summary>
public sealed class FishingSchoolDefinition
{
    public int Id { get; set; }

    public float[] Position { get; set; } = [];

    public int ModelId { get; set; }

    public List<int> Fish { get; set; } = [];
}
