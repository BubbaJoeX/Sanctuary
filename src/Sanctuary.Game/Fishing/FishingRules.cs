using System;
using System.Collections.Generic;
using System.Linq;

using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Fishing;

public enum FishingCatchKind
{
    /// <summary>
    /// Fish swim round the lure but none bites.
    /// </summary>
    Nothing,
    Fish,
    Junk,
    Treasure
}

/// <summary>
/// What a cast will bring up, rolled as it lands.
/// </summary>
public sealed record FishingCatch(FishingCatchKind Kind, FishDefinition? Fish = null, FishSize Size = FishSize.Small,
    float Weight = 0f, int ItemId = 0, FishingTreasureDefinition? Treasure = null)
{
    public bool IsItem => Kind is FishingCatchKind.Junk or FishingCatchKind.Treasure;

    public int Rarity => Fish?.Rarity ?? 1;
}

/// <summary>
/// The pond, the cast and the fisher, as the catch roll sees them.
/// </summary>
public sealed class FishingCastContext
{
    public required IReadOnlyList<FishDefinition> PondFish { get; init; }

    /// <summary>
    /// The fish of the school the cast landed on, if any.
    /// </summary>
    public IReadOnlyCollection<int> SchoolFish { get; init; } = [];

    public IReadOnlyList<FishingJunkDefinition> Junk { get; init; } = [];

    public IReadOnlyList<FishingTreasureDefinition> Treasure { get; init; } = [];

    /// <summary>
    /// The lure in use (an item id), 0 for none.
    /// </summary>
    public int LureItemId { get; init; }

    /// <summary>
    /// The Treasure Magnet is in use.
    /// </summary>
    public bool TreasureLure { get; init; }

    public int RodTier { get; init; }
}

/// <summary>
/// How fishing plays: what bites and when. The client only animates; all of this is the server's.
/// </summary>
public static class FishingRules
{
    public const int FishermanProfileId = 137;

    /// <summary>
    /// The client's fishing state, a bit of the character state (0x00400000): set, the client turns on its
    /// fishing camera and casting; cleared, it stops.
    /// </summary>
    public const int FishingCharacterState = 0x00400000;

    public const float MinCastDistance = 3f;

    /// <summary>
    /// The client measures its cast range from the fisher's feet in 3D; the server allows for that.
    /// </summary>
    public const float CastDistanceSlack = 2f;

    /// <summary>
    /// A cast must land this close to the pond's surface. The client only casts at water itself; this refuses
    /// casts that didn't come from it.
    /// </summary>
    public const float WaterHeightTolerance = 4f;

    /// <summary>
    /// A cast this close to a school (across the water) lands on it.
    /// </summary>
    public const float SchoolRadius = 7f;

    /// <summary>
    /// The bobber flies for 2 seconds; then the client cuts to the lure underwater.
    /// </summary>
    public const int FishRunDelayMs = 2200;

    public const int NibbleDelayMinMs = 2000;
    public const int NibbleDelayMaxMs = 5000;

    public const int BiteDelayMinMs = 1500;
    public const int BiteDelayMaxMs = 3000;

    /// <summary>
    /// The client won't send a reel while its bite plays, so the window opens after it.
    /// </summary>
    public const int BiteLockMs = 1200;

    /// <summary>
    /// A chest or a boot sits by the lure this long before it's lost.
    /// </summary>
    public const int ItemWaitMs = 8000;

    /// <summary>
    /// With nothing biting, the cast ends in "nothing caught" after this.
    /// </summary>
    public const int EmptyRunMs = 10000;

    /// <summary>
    /// How much more often a lure (or the Treasure Magnet) brings up what it's for, as a share of all casts
    /// ("a 10% better chance").
    /// </summary>
    public const double LureBonus = 0.1;

    private static readonly double[] RarityWeights = [0, 10, 5, 2, 0.4];
    private static readonly double[] SizeWeights = [0, 40, 35, 18, 7];

    private const double SchoolFishMultiplier = 4;
    private const double JunkShare = 0.15;
    private const double TreasureShare = 0.08;
    private const double OpenWaterNothingShare = 0.12;
    private const double SchoolNothingShare = 0.03;

    /// <summary>
    /// How long the fish stays on the line after its bite, once the client lets the fisher reel.
    /// </summary>
    public static int BiteWindowMs(int rarity, int rodTier) => 2600 - 400 * (Math.Clamp(rarity, 1, 4) - 1) + 100 * rodTier;

    /// <summary>
    /// How long it takes to land a hooked fish (or chest).
    /// </summary>
    public static int ReelMs(int rarity, int rodTier) => Math.Max(1000, 1500 + 500 * (Math.Clamp(rarity, 1, 4) - 1) - 100 * rodTier);

    /// <summary>
    /// The chance a hooked rare or legendary fish breaks free while being reeled in.
    /// </summary>
    public static double ReelEscapeChance(int rarity, int rodTier) =>
        Math.Max(0, (rarity switch { 3 => 0.10, 4 => 0.25, _ => 0 }) - 0.03 * rodTier);

    public static FishingCatch Roll(FishingCastContext context, Random random)
    {
        var options = new List<(double Weight, Func<FishingCatch> Build)>();

        var onSchool = context.SchoolFish.Count > 0;
        var lured = new List<int>();

        foreach (var fish in context.PondFish)
        {
            var weight = RarityWeights[Math.Clamp(fish.Rarity, 1, 4)];

            if (context.SchoolFish.Contains(fish.Id))
                weight *= SchoolFishMultiplier;

            if (context.LureItemId != 0 && fish.LureItemId == context.LureItemId)
                lured.Add(options.Count);

            options.Add((weight, () => RollFish(fish, context.RodTier, random)));
        }

        var fishTotal = options.Sum(option => option.Weight);

        if (context.Junk.Count > 0)
        {
            var share = fishTotal * JunkShare * (onSchool ? 0.5 : 1) / context.Junk.Count;

            foreach (var junk in context.Junk)
                options.Add((share, () => new FishingCatch(FishingCatchKind.Junk, ItemId: junk.ItemId)));
        }

        var treasure = new List<int>();

        if (context.Treasure.Count > 0)
        {
            var treasureWeight = context.Treasure.Sum(entry => entry.Weight);

            foreach (var entry in context.Treasure)
            {
                treasure.Add(options.Count);
                options.Add((fishTotal * TreasureShare * entry.Weight / treasureWeight,
                    () => new FishingCatch(FishingCatchKind.Treasure, Size: entry.Size, Treasure: entry)));
            }
        }

        options.Add((fishTotal * (onSchool ? SchoolNothingShare : OpenWaterNothingShare), () => new FishingCatch(FishingCatchKind.Nothing)));

        // A lure adds a tenth of all casts to its fish, shared between them in their usual proportions.
        AddBonus(options, lured);

        if (context.TreasureLure)
            AddBonus(options, treasure);

        var roll = random.NextDouble() * options.Sum(option => option.Weight);

        foreach (var (weight, build) in options)
        {
            if (roll < weight)
                return build();

            roll -= weight;
        }

        return options[^1].Build();
    }

    private static void AddBonus(List<(double Weight, Func<FishingCatch> Build)> options, List<int> favored)
    {
        var favoredWeight = favored.Sum(index => options[index].Weight);

        if (favoredWeight <= 0)
            return;

        var bonus = LureBonus * options.Sum(option => option.Weight) / (1 - LureBonus);

        foreach (var index in favored)
            options[index] = (options[index].Weight + bonus * options[index].Weight / favoredWeight, options[index].Build);
    }

    /// <summary>
    /// A fish's size (among those it comes in; better rods find bigger ones) and its weight.
    /// </summary>
    public static FishingCatch RollFish(FishDefinition fish, int rodTier, Random random)
    {
        var sizes = fish.Items.Keys.OrderBy(size => size).ToList();

        var weights = sizes.Select(size => SizeWeights[(int)size] * (1 + 0.25 * rodTier * ((int)size - 1))).ToList();

        var roll = random.NextDouble() * weights.Sum();
        var size = sizes[^1];

        for (var i = 0; i < sizes.Count; i++)
        {
            if (roll < weights[i])
            {
                size = sizes[i];
                break;
            }

            roll -= weights[i];
        }

        return new FishingCatch(FishingCatchKind.Fish, fish, size, RollWeight(size, fish.Rarity, random), fish.Items[size]);
    }

    public static float RollWeight(FishSize size, int rarity, Random random)
    {
        var (low, high) = size switch
        {
            FishSize.Small => (0.5, 2.0),
            FishSize.Medium => (2.0, 5.0),
            FishSize.Large => (5.0, 10.0),
            _ => (10.0, 20.0)
        };

        var weight = low + random.NextDouble() * (high - low);

        return (float)Math.Round(rarity >= 4 ? weight * 1.5 : weight, 2);
    }

    /// <summary>
    /// The catch log's score.
    /// </summary>
    public static int Score(FishingCatch fishingCatch) => fishingCatch.Kind switch
    {
        FishingCatchKind.Fish => 25 * fishingCatch.Rarity + 10 * (int)fishingCatch.Size + (int)fishingCatch.Weight,
        FishingCatchKind.Treasure => 20 + 10 * (int)fishingCatch.Size,
        FishingCatchKind.Junk => 1,
        _ => 0
    };

    /// <summary>
    /// The Fisherman job's stars for a catch.
    /// </summary>
    public static int Stars(FishingCatch fishingCatch) => fishingCatch.Kind switch
    {
        FishingCatchKind.Fish => 6 + 6 * fishingCatch.Rarity + 3 * (int)fishingCatch.Size,
        FishingCatchKind.Treasure => 8 + 2 * (int)fishingCatch.Size,
        FishingCatchKind.Junk => 3,
        _ => 0
    };

    /// <summary>
    /// The model a caught fish is held up as, by its size.
    /// </summary>
    public static int CaughtFishModelId(FishSize size) => size switch
    {
        FishSize.Small => 1623,     // sg_fish_catch_small_bbe
        FishSize.Medium => 1621,    // sg_fish_catch_medium_bbe
        FishSize.Large => 1620,     // sg_fish_catch_large_bbe
        _ => 1622                   // sg_fish_catch_xlarge_bbe
    };

    public const int TreasureChestModelId = 1624;   // sg_fishing_treasure_chest_bbe
    public const int CaughtJunkModelId = 1565;      // sg_fish_catch_bbe
    public const int LureModelId = 1673;            // sg_fishing_lure_bbe
    public const int StarterBobberModelId = 1641;   // sg_fishing_bobber01_bbe

    public const int CatchFishEffectId = 15880;     // BBE_PFX_fishing_moneyshot_fish
    public const int CatchChestEffectId = 15881;    // BBE_PFX_fishing_moneyshot_chest
    public const int BobberSplashEffectId = 15882;  // BBE_PFX_fishing_bobber_splash
    public const int SchoolEffectId = 15777;        // BBE_PFX_fishing_school

    /// <summary>
    /// The Fish Finder's highlight on a school: green for common fish, yellow for uncommon, red for rare.
    /// </summary>
    public static int FishFinderEffectId(int rarity) => rarity switch
    {
        1 => 16086,     // BBE_PFX_fishing_fishfinder_green
        2 => 16087,     // BBE_PFX_fishing_fishfinder_yellow
        _ => 16085      // BBE_PFX_fishing_fishfinder_red
    };

    /// <summary>
    /// The underwater view: a cast d metres long shows at x = offset + d * scale of the strip every fishing zone
    /// keeps at z 485, which runs from about x 40 to 75.
    /// </summary>
    public const float UnderwaterRunScale = 1.6f;
    public const float UnderwaterRunOffset = 38f;
}
