using System;
using System.Collections.Generic;

namespace Sanctuary.Game.Minigames.MatchThree;

/// <summary>
/// Weighted picks (the movies' core.Randomizer): elements with chances, one drawn at random.
/// </summary>
public sealed class MatchThreeRandomizer(Random random)
{
    private sealed class Entry
    {
        public int Element;
        public double Chance;
        public double Odds = double.NaN;
    }

    private readonly List<Entry> _entries = [];

    private double _totalChance;

    /// <summary>
    /// Adds an element; a chance that's missing (a blank column) or not above 0 adds nothing, as in the movie.
    /// </summary>
    public void AddElement(int element, int? chance)
    {
        if (chance is not > 0)
            return;

        _totalChance += chance.Value;
        _entries.Add(new Entry { Element = element, Chance = chance.Value });
    }

    /// <summary>
    /// Adds a "one in so many" element (loot drops): the total is the largest odds, each element's chance the total
    /// over its odds.
    /// </summary>
    public void AddOdds(int element, int odds, int multiplier)
    {
        var scaled = (double)odds * multiplier;

        if (scaled > _totalChance)
            _totalChance = scaled;

        _entries.Add(new Entry { Element = element, Odds = scaled });

        foreach (var entry in _entries)
            entry.Chance = _totalChance / entry.Odds;
    }

    /// <summary>
    /// An element by weight, or -1 when nothing can come up.
    /// </summary>
    public int GetElement()
    {
        if (_totalChance == 0)
            return -1;

        var remaining = _totalChance;
        var roll = Math.Floor(random.NextDouble() * _totalChance);

        foreach (var entry in _entries)
        {
            remaining -= entry.Chance;

            if (roll >= remaining)
                return entry.Element;
        }

        return -1;
    }

    public void ChangeElementChance(int element, double chance)
    {
        foreach (var entry in _entries)
        {
            if (entry.Element != element)
                continue;

            _totalChance -= entry.Chance;
            entry.Chance = chance;
            _totalChance += chance;
            break;
        }
    }
}

/// <summary>
/// Makes the level's tiles (the movies' core.ServerTileGenerator): which category comes up by the level's spawn
/// chances, then which tile of it by its spawn rows. Creating it tells the movie the art for each value
/// (OnTileInfoMsg), which it needs before it can draw the board.
/// </summary>
public sealed class MatchThreeTileGenerator
{
    /// <summary>
    /// Loot drops come up one in (their spawn chance times this).
    /// </summary>
    public const int LootRate = 10;

    /// <summary>
    /// A level whose chances only pick categories it has no rows for would loop forever in the movie; this stops it.
    /// </summary>
    private const int MaximumRetries = 100;

    private readonly MatchThreeLevel _level;
    private readonly Random _random;

    private readonly MatchThreeRandomizer _lootDropRandomizer;
    private readonly MatchThreeRandomizer _collectableRandomizer;
    private readonly MatchThreeRandomizer _bonusRandomizer;
    private readonly MatchThreeRandomizer _selectableRandomizer;
    private readonly MatchThreeRandomizer _powerupRandomizer;
    private readonly MatchThreeRandomizer _hazardRandomizer;
    private readonly MatchThreeRandomizer _blockerRandomizer;
    private readonly MatchThreeRandomizer _wildcardRandomizer;

    private MatchThreeRandomizer _typeRandomizer = null!;
    private MatchThreeRandomizer _regularRandomizer = null!;

    public MatchThreeTileGenerator(MatchThreeLevel level, MatchThreeGame game)
    {
        _level = level;
        _random = game.Random;

        SetBaseRandomizers();

        _lootDropRandomizer = new MatchThreeRandomizer(_random);

        foreach (var data in level.LootDropSpawnData.Values)
        {
            _lootDropRandomizer.AddOdds(MatchThreeTiles.LootDrop + data.Type, data.SpawnChance, LootRate);
            game.SendTileInfo(MatchThreeTiles.LootDrop + data.Type, data.Type);
        }

        _collectableRandomizer = new MatchThreeRandomizer(_random);

        foreach (var data in level.CollectableSpawnData.Values)
        {
            _collectableRandomizer.AddElement(MatchThreeTiles.Collectable + data.Type, data.SpawnChance);
            game.SendTileInfo(MatchThreeTiles.Collectable + data.Type, data.Type);
        }

        _bonusRandomizer = new MatchThreeRandomizer(_random);

        foreach (var data in level.BonusSpawnData.Values)
        {
            _bonusRandomizer.AddElement(MatchThreeTiles.Bonus + data.Type, data.SpawnChance);
            game.SendTileInfo(MatchThreeTiles.Bonus + data.Type, data.Type);
        }

        _selectableRandomizer = new MatchThreeRandomizer(_random);

        foreach (var data in level.SelectableSpawnData.Values)
        {
            _selectableRandomizer.AddElement(MatchThreeTiles.Selectable + data.Type, data.SpawnChance);
            game.SendTileInfo(MatchThreeTiles.Selectable + data.Type, data.Type);
        }

        _powerupRandomizer = new MatchThreeRandomizer(_random);

        foreach (var data in level.PowerupSpawnData.Values)
            _powerupRandomizer.AddElement(data.Value, data.SpawnChance);

        _hazardRandomizer = new MatchThreeRandomizer(_random);
        _hazardRandomizer.AddElement(MatchThreeTiles.HazardLock, 100);

        _blockerRandomizer = new MatchThreeRandomizer(_random);
        _blockerRandomizer.AddElement(MatchThreeTiles.Blocker, 100);

        _wildcardRandomizer = new MatchThreeRandomizer(_random);
        _wildcardRandomizer.AddElement(MatchThreeTiles.Wildcard, 100);
    }

    /// <summary>
    /// The category chances, from the level and its wave: "any" tiles pick from all of them, "regular" ones only
    /// from selectables, locked tiles and wildcards.
    /// </summary>
    public void SetBaseRandomizers()
    {
        _typeRandomizer = new MatchThreeRandomizer(_random);
        _typeRandomizer.AddElement(MatchThreeTiles.Collectable, _level.GetCollectableSpawnChance());
        _typeRandomizer.AddElement(MatchThreeTiles.Bonus, _level.GetBonusSpawnChance());
        _typeRandomizer.AddElement(MatchThreeTiles.Selectable, _level.SelectableSpawnChance);
        _typeRandomizer.AddElement(MatchThreeTiles.Powerup, _level.GetPowerupSpawnChance());
        _typeRandomizer.AddElement(MatchThreeTiles.Blocker, _level.GetBlockerSpawnChance());
        _typeRandomizer.AddElement(MatchThreeTiles.Hazard, _level.GetHazardSpawnChance());
        _typeRandomizer.AddElement(MatchThreeTiles.Wildcard, _level.GetWildcardSpawnChance());

        _regularRandomizer = new MatchThreeRandomizer(_random);
        _regularRandomizer.AddElement(MatchThreeTiles.Selectable, _level.SelectableSpawnChance);
        _regularRandomizer.AddElement(MatchThreeTiles.Hazard, _level.GetHazardSpawnChance());
        _regularRandomizer.AddElement(MatchThreeTiles.Wildcard, _level.GetWildcardSpawnChance());
    }

    /// <summary>
    /// A tile of a category (or of the build kinds <see cref="MatchThreeTiles.Any"/>,
    /// <see cref="MatchThreeTiles.Regular"/>, <see cref="MatchThreeTiles.BuildAny"/>,
    /// <see cref="MatchThreeTiles.BuildRegular"/>). A category that can't make one (its rows are used up) gives way
    /// to any tile.
    /// </summary>
    public MatchThreeTile GenerateTile(int category, int value)
    {
        for (var attempt = 0; attempt < MaximumRetries; attempt++)
        {
            if (category == MatchThreeTiles.Any)
            {
                value = _lootDropRandomizer.GetElement();
                category = value >= MatchThreeTiles.LootDrop ? MatchThreeTiles.LootDrop : _typeRandomizer.GetElement();
            }
            else if (category is MatchThreeTiles.Regular or MatchThreeTiles.BuildRegular)
            {
                category = _regularRandomizer.GetElement();
            }
            else if (category == MatchThreeTiles.BuildAny)
            {
                category = _typeRandomizer.GetElement();
            }

            var tile = category switch
            {
                MatchThreeTiles.Empty => new MatchThreeTile(category, MatchThreeTiles.Empty),
                MatchThreeTiles.DropZone => new MatchThreeTile(category, MatchThreeTiles.DropZone),
                MatchThreeTiles.Blocker => FromRandomizer(category, _blockerRandomizer),
                MatchThreeTiles.Collectable => GenerateCounted(category, _collectableRandomizer),
                MatchThreeTiles.Bonus => GenerateCounted(category, _bonusRandomizer),
                MatchThreeTiles.Powerup => FromRandomizer(category, _powerupRandomizer),
                MatchThreeTiles.Hazard => GenerateHazard(),
                MatchThreeTiles.Wildcard => FromRandomizer(category, _wildcardRandomizer),
                MatchThreeTiles.Selectable => GenerateCounted(category, _selectableRandomizer),
                MatchThreeTiles.LootDrop => new MatchThreeTile(category, value),
                _ => null
            };

            if (tile is not null)
                return tile;

            category = MatchThreeTiles.Any;
        }

        throw new InvalidOperationException($"Match-three level {_level.LevelId} can't make a tile: its spawn chances pick nothing it has rows for.");
    }

    /// <summary>
    /// A tile the board needs as it is (a wildcard from a powerup).
    /// </summary>
    public static MatchThreeTile AddSpecificTile(int category, int value) => new(category, value);

    private static MatchThreeTile? FromRandomizer(int category, MatchThreeRandomizer randomizer)
    {
        var value = randomizer.GetElement();
        return value == -1 ? null : new MatchThreeTile(category, value);
    }

    /// <summary>
    /// Selectables, collectables and bonuses count against their row's create limit; the last one allowed stops
    /// the row spawning.
    /// </summary>
    private MatchThreeTile? GenerateCounted(int category, MatchThreeRandomizer randomizer)
    {
        var value = randomizer.GetElement();

        if (value == -1)
            return null;

        if (!_level.CreatedTile(category, value))
            randomizer.ChangeElementChance(value, 0);

        return new MatchThreeTile(category, value);
    }

    /// <summary>
    /// A hazard is a locked selectable.
    /// </summary>
    private MatchThreeTile? GenerateHazard()
    {
        if (_hazardRandomizer.GetElement() != MatchThreeTiles.HazardLock)
            return null;

        var tile = GenerateCounted(MatchThreeTiles.Selectable, _selectableRandomizer);
        tile?.Lock();

        return tile;
    }
}
