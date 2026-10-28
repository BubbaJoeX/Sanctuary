using System.Collections.Generic;

namespace Sanctuary.Game.Minigames.MatchThree;

/// <summary>
/// A level's waves (the movies' Progression, from MatchThreeProgression.txt): every so many collected tiles a new
/// wave starts, which raises some spawn chances (blockers, bonuses, powerups, ...) and refreshes the tile queue.
/// Without a progression row the level still starts a new wave every 100 tiles; it just changes nothing.
/// </summary>
public sealed class MatchThreeProgression
{
    public const int PropertyCount = 7;

    /// <summary>
    /// The row that turns waves on: its init is the tiles per wave, its delta how many properties a wave raises,
    /// its max the last wave (-1 for no limit).
    /// </summary>
    public const int PropertyProgression = -1;
    public const int UnlimitedWaves = -1;

    public const int PropertyBlocker = 0;
    public const int PropertyBonus = 1;
    public const int PropertyPowerup = 2;
    public const int PropertyWildcard = 3;
    public const int PropertyHazard = 4;
    public const int PropertyCollectable = 5;
    public const int PropertyTimerChain = 6;

    private readonly Dictionary<int, int> _property = [];
    private readonly Dictionary<int, int> _propertyIncrease = [];
    private readonly Dictionary<int, int> _propertyMax = [];

    private int _propertiesToChange = PropertyCount;
    private int _tilesPerWave = 100;
    private int _maxWaves = UnlimitedWaves;
    private int _tileCount;
    private int _nextProperty;

    public bool IsEnabled { get; private set; }

    public int Wave { get; private set; } = 1;

    /// <summary>
    /// What a wave adds to a spawn chance. The movie's version gives NaN for a property its row doesn't list (which
    /// would stop that kind spawning altogether); here that's 0.
    /// </summary>
    public int GetProperty(int type)
    {
        if (!IsEnabled)
            return 0;

        return _property.GetValueOrDefault(type);
    }

    public bool NextWave()
    {
        _tileCount = 0;

        if (Wave > _maxWaves && _maxWaves > UnlimitedWaves)
            return false;

        Wave++;

        var changed = 0;
        var first = _nextProperty;

        while (changed < _propertiesToChange)
        {
            if (IncreaseProperty(_nextProperty))
                changed++;

            _nextProperty = (_nextProperty + 1) % PropertyCount;

            if (_nextProperty == first)
                break;
        }

        return true;
    }

    /// <summary>
    /// Counts a collected tile; true when that makes a wave.
    /// </summary>
    public bool AddTile()
    {
        _tileCount++;
        return _tileCount >= _tilesPerWave;
    }

    public void InitProperty(int type, int init, int increase, int max)
    {
        if (type == PropertyProgression)
        {
            IsEnabled = init > 0;
            _propertiesToChange = increase;
            _tilesPerWave = init;
            _maxWaves = max;
            return;
        }

        _propertyIncrease[type] = increase;
        _propertyMax[type] = max;
        _property[type] = init;
    }

    private bool IncreaseProperty(int type)
    {
        if (!_property.TryGetValue(type, out var old))
            return false;

        var value = old + _propertyIncrease.GetValueOrDefault(type);
        var max = _propertyMax.GetValueOrDefault(type);

        if (max < 0 && value < max)
            value = max;
        else if (max > 0 && value > max)
            value = max;
        else if (max == 0)
            value = 0;

        _property[type] = value;
        return value != old;
    }
}
