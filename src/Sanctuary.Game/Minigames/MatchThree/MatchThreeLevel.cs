using System.Collections.Generic;
using System.Linq;

using Sanctuary.Game.Minigames.Soe;

namespace Sanctuary.Game.Minigames.MatchThree;

/// <summary>
/// A level's score and its multiplier (the movies' core.Score). Each change goes to the movie as it happens.
/// </summary>
public sealed class MatchThreeScore(MatchThreeGame game)
{
    private int _multiplierTimer;
    private int _multiplierDuration = 10000;

    public int Score { get; private set; }

    public int Multiplier { get; private set; } = 1;

    public void UpdateScore(int points, int reason)
    {
        var delta = points * Multiplier;
        Score += delta;

        game.AddScore(delta);
        game.SendScoreIncrement(delta, reason);
    }

    public void ResetMultiplier()
    {
        Multiplier = 1;
        _multiplierTimer = 0;
        _multiplierDuration = 0;

        game.SendScoreMultiplierExpired();
    }

    public void ApplyTick(long delta)
    {
        if (Multiplier == 1)
            return;

        _multiplierTimer += (int)delta;

        if (_multiplierTimer != 0 && _multiplierTimer > _multiplierDuration)
            ResetMultiplier();
    }

    /// <summary>
    /// A score powerup: a bigger multiplier replaces the current one, an equal or smaller one only restarts it.
    /// </summary>
    public void UpdateMultiplier(int multiplier, int seconds)
    {
        if (multiplier > Multiplier)
        {
            Multiplier = multiplier;
            _multiplierTimer = 0;
            _multiplierDuration = seconds * 1000;
        }
        else
        {
            _multiplierTimer = 0;
        }

        game.SendScoreMultiplier(Multiplier, _multiplierDuration);
    }
}

/// <summary>
/// A level of a match-three game (the movies' GameLevel, from a MatchThreeLevelData.txt row): its board, timer,
/// spawn chances and the tile kinds it spawns, with their quotas. It answers the movie's moves on its board and
/// counts what they collect; it succeeds when every quota is met.
/// </summary>
public sealed class MatchThreeLevel : SoeLevel
{
    private readonly MatchThreeGame _game;

    private readonly Dictionary<int, int> _selectableCollected = [];
    private readonly Dictionary<int, int> _selectableCreated = [];
    private readonly Dictionary<int, int> _collectableCollected = [];
    private readonly Dictionary<int, int> _collectableCreated = [];
    private readonly Dictionary<int, int> _bonusCollected = [];
    private readonly Dictionary<int, int> _bonusCreated = [];
    private readonly Dictionary<int, int> _lootDropCollected = [];
    private readonly Dictionary<int, int> _lootDropCreated = [];

    public MatchThreeLevel(MatchThreeGame game) : base(game)
    {
        _game = game;
        LevelScore = new MatchThreeScore(game);
    }

    public int LevelId { get; set; }

    /// <summary>
    /// The level's number in its game, from 1.
    /// </summary>
    public int Index { get; set; }

    public int ObjectiveId { get; set; }

    public MatchThreeLevelType Type { get; set; }

    /// <summary>
    /// The type data column: the movie's mold meter style.
    /// </summary>
    public int MoldId { get; set; }

    public int GridId { get; set; }

    public int[][] GridShape { get; set; } = [];

    public bool CollectableStopAtQuota { get; set; }
    public bool SelectableStopAtQuota { get; set; }

    // The category chances; a blank column (null) leaves that category out.
    public int? CollectableSpawnChance { get; set; } = 0;
    public int? BonusSpawnChance { get; set; } = 0;
    public int? SelectableSpawnChance { get; set; } = 80;
    public int? PowerupSpawnChance { get; set; } = 10;
    public int? HazardSpawnChance { get; set; } = 0;
    public int? BlockerSpawnChance { get; set; } = 5;
    public int? WildcardSpawnChance { get; set; } = 5;

    public int ScoreBest => 0;

    public MatchThreeProgression Progression { get; } = new();

    /// <summary>
    /// The level's points (the framework's level score stays 0, as in the movie).
    /// </summary>
    public MatchThreeScore LevelScore { get; }

    /// <summary>
    /// The board, once the level has started.
    /// </summary>
    public MatchThreeGrid? Grid { get; private set; }

    // By type (a powerup's by value), in order.
    public SortedDictionary<int, MatchThreeSpawnData> SelectableSpawnData { get; } = [];
    public SortedDictionary<int, MatchThreeSpawnData> CollectableSpawnData { get; } = [];
    public SortedDictionary<int, MatchThreeSpawnData> BonusSpawnData { get; } = [];
    public SortedDictionary<int, MatchThreeSpawnData> PowerupSpawnData { get; } = [];
    public SortedDictionary<int, MatchThreeSpawnData> LootDropSpawnData { get; } = [];

    public int? GetCollectableSpawnChance() => CollectableSpawnChance + Progression.GetProperty(MatchThreeProgression.PropertyCollectable);
    public int? GetBonusSpawnChance() => BonusSpawnChance + Progression.GetProperty(MatchThreeProgression.PropertyBonus);
    public int? GetPowerupSpawnChance() => PowerupSpawnChance + Progression.GetProperty(MatchThreeProgression.PropertyPowerup);
    public int? GetHazardSpawnChance() => HazardSpawnChance + Progression.GetProperty(MatchThreeProgression.PropertyHazard);
    public int? GetBlockerSpawnChance() => BlockerSpawnChance + Progression.GetProperty(MatchThreeProgression.PropertyBlocker);
    public int? GetWildcardSpawnChance() => WildcardSpawnChance + Progression.GetProperty(MatchThreeProgression.PropertyWildcard);

    public int CollectedSelectables(int type) => _selectableCollected.GetValueOrDefault(type);
    public int CollectedCollectables(int type) => _collectableCollected.GetValueOrDefault(type);
    public int CollectedBonuses(int type) => _bonusCollected.GetValueOrDefault(type);

    #region Spawn rows

    public void AddCollectableSpawnData(MatchThreeSpawnData data)
    {
        data.Value = MatchThreeTiles.Collectable + data.Type;
        CollectableSpawnData[data.Type] = data;
    }

    public void AddBonusSpawnData(MatchThreeSpawnData data)
    {
        data.Value = MatchThreeTiles.Bonus + data.Type;
        BonusSpawnData[data.Type] = data;
    }

    public void AddSelectableSpawnData(MatchThreeSpawnData data)
    {
        data.Value = MatchThreeTiles.Selectable + data.Type;
        SelectableSpawnData[data.Type] = data;
    }

    public void AddLootDropSpawnData(MatchThreeSpawnData data)
    {
        data.Value = MatchThreeTiles.LootDrop + data.Type;
        LootDropSpawnData[data.Type] = data;
    }

    /// <summary>
    /// Only the powerups the movies have: time freeze, the score multipliers, dynamite, wild tiles, destruct and
    /// shuffle.
    /// </summary>
    public void AddPowerupSpawnData(MatchThreeSpawnData data)
    {
        var value = MatchThreeTiles.Powerup + data.Type;
        data.Value = value;

        if (value is MatchThreeTiles.PowerupFreeze or MatchThreeTiles.PowerupScore2X or MatchThreeTiles.PowerupScore5X
            or MatchThreeTiles.PowerupDynamite or MatchThreeTiles.PowerupWildTile or MatchThreeTiles.PowerupDestruct
            or MatchThreeTiles.PowerupShuffle)
        {
            PowerupSpawnData[value] = data;
        }
    }

    public void AddProgressionData(int property, int init, int delta, int max)
    {
        Progression.InitProperty(property, init, delta, max);
    }

    #endregion

    #region Level (GameLevel)

    public void ApplyTick(long delta)
    {
        LevelScore.ApplyTick(delta);
    }

    /// <summary>
    /// The movie's GameLevel.StartLevel: the generator (which sends the tile art), the board (which sends the tile
    /// queue, the board and the random queue), the timer, the bins and the powerups' strengths.
    /// </summary>
    public override void OnStartLevel()
    {
        _selectableCollected.Clear();
        _selectableCreated.Clear();
        _collectableCollected.Clear();
        _collectableCreated.Clear();
        _bonusCollected.Clear();
        _bonusCreated.Clear();
        _lootDropCollected.Clear();
        _lootDropCreated.Clear();

        var generator = new MatchThreeTileGenerator(this, _game);
        Grid = new MatchThreeGrid(this, generator, GridShape, _game);

        _game.SendLevelInit(Level, EndDuration, (int)Type);
        QuotasInit();

        foreach (var data in PowerupSpawnData.Values)
            _game.SendPowerupInit(data.Value, data.Duration, data.Power);
    }

    /// <summary>
    /// Whether the level is over: its time is up, or (for a stop-at-quota level) every quota is met.
    /// </summary>
    public bool IsLevelDone()
    {
        if (CheckDone())
            return true;

        if (!IsStarted)
            return false;

        if (IsDone)
            return true;

        if (CollectableStopAtQuota && CheckCollectableQuota(false))
            return true;

        return SelectableStopAtQuota && CheckSelectableQuota(false);
    }

    public bool CheckSuccess()
    {
        return CheckCollectableQuota(true) && CheckSelectableQuota(true);
    }

    /// <summary>
    /// Counts a tile made against its row's create limit; false once that's reached.
    /// </summary>
    public bool CreatedTile(int category, int value)
    {
        switch (category)
        {
            case MatchThreeTiles.Collectable:
                return Created(CollectableSpawnData, _collectableCreated, value - MatchThreeTiles.Collectable);

            case MatchThreeTiles.Selectable:
                return Created(SelectableSpawnData, _selectableCreated, value - MatchThreeTiles.Selectable);

            case MatchThreeTiles.Bonus:
                return Created(BonusSpawnData, _bonusCreated, value - MatchThreeTiles.Bonus);

            case MatchThreeTiles.LootDrop:
                return Created(LootDropSpawnData, _lootDropCreated, value - MatchThreeTiles.LootDrop);

            default:
                return true;
        }
    }

    private static bool Created(SortedDictionary<int, MatchThreeSpawnData> spawnData, Dictionary<int, int> created, int type)
    {
        var count = created.GetValueOrDefault(type) + 1;
        created[type] = count;

        return !spawnData.TryGetValue(type, out var data) || data.WithinCreateLimit(count);
    }

    /// <summary>
    /// A chain adds two seconds a tile (and the wave's timer bonus), at least one.
    /// </summary>
    public double CalculateChainTimeBonus(int chainLength)
    {
        double seconds = 2 * chainLength;

        if (seconds < 0)
            return 0;

        if (seconds > 0)
        {
            seconds += Progression.GetProperty(MatchThreeProgression.PropertyTimerChain) * 0.1;

            if (seconds < MatchThreeTiles.MinimumChainTimeBonus)
                seconds = MatchThreeTiles.MinimumChainTimeBonus;

            _game.SendTimerIncrement(seconds, MatchThreeTiles.TimerChain);
            ChangeTimerDuration(seconds);
        }

        return seconds;
    }

    /// <summary>
    /// A chain scores 400, and 200 for each tile past three.
    /// </summary>
    public int CalculateChainScore(int chainLength)
    {
        var points = MatchThreeTiles.ChainScore;

        if (chainLength > MatchThreeTiles.MinimumChainLength)
            points += (chainLength - MatchThreeTiles.MinimumChainLength) * MatchThreeTiles.ChainBonusTileScore;

        LevelScore.UpdateScore(points, MatchThreeTiles.ScoreChain);
        return points;
    }

    /// <summary>
    /// A tile was collected (by a chain or a drop zone): its quota, its score, and the wave count.
    /// </summary>
    public void HandleCollected(int category, int value)
    {
        switch (category)
        {
            case MatchThreeTiles.Collectable:
                HandleCollectableTile(value);
                break;

            case MatchThreeTiles.Bonus:
                HandleBonusTile(value);
                break;

            case MatchThreeTiles.Powerup:
                HandlePowerupTile(value);
                break;

            case MatchThreeTiles.Selectable:
                HandleSelectableTile(value);
                break;

            case MatchThreeTiles.Wildcard:
                // A wildcard took its chain's value.
                if (value >= MatchThreeTiles.Selectable)
                    HandleSelectableTile(value);
                break;

            case MatchThreeTiles.LootDrop:
                HandleLootDropTile(value);
                break;
        }

        if (Progression.AddTile() && Progression.NextWave())
        {
            Grid?.NextWave();
            _game.SendNextWave(Progression.Wave);
        }
    }

    private void HandleCollectableTile(int value)
    {
        var type = value - MatchThreeTiles.Collectable;

        if (!CollectableSpawnData.TryGetValue(type, out var data) || !CountCollected(data, _collectableCollected, type))
            return;

        // The movie works out a collectable score here (doubled past the quota) but never adds it.
        if (data.Quota > 0)
            _game.SendQuotaUpdate(data.Index, _collectableCollected[type], data.Quota, data.RequiredPerReward);
    }

    private void HandleBonusTile(int value)
    {
        var type = value - MatchThreeTiles.Bonus;

        if (!BonusSpawnData.TryGetValue(type, out var data) || !CountCollected(data, _bonusCollected, type))
            return;

        LevelScore.UpdateScore(MatchThreeTiles.BonusScore, MatchThreeTiles.ScoreBonus);
    }

    private void HandleLootDropTile(int value)
    {
        var type = value - MatchThreeTiles.LootDrop;

        if (!LootDropSpawnData.TryGetValue(type, out var data) || !CountCollected(data, _lootDropCollected, type))
            return;

        LevelScore.UpdateScore(MatchThreeTiles.BonusScore, MatchThreeTiles.ScoreBonus);
    }

    /// <summary>
    /// Counts one more collected, unless the row's collect limit is reached.
    /// </summary>
    private static bool CountCollected(MatchThreeSpawnData data, Dictionary<int, int> collected, int type)
    {
        var count = collected.GetValueOrDefault(type);

        if (data.LimitToCollectLimit(count + 1) - count <= 0)
            return false;

        collected[type] = count + 1;
        return true;
    }

    private void HandleSelectableTile(int value)
    {
        var type = value - MatchThreeTiles.Selectable;
        var count = _selectableCollected.GetValueOrDefault(type) + 1;
        _selectableCollected[type] = count;

        if (SelectableSpawnData.TryGetValue(type, out var data) && data.Quota > 0)
            _game.SendQuotaUpdate(data.Index, count, data.Quota, data.RequiredPerReward);
    }

    private void HandlePowerupTile(int value)
    {
        if (!PowerupSpawnData.TryGetValue(value, out var data))
            return;

        switch (value)
        {
            case MatchThreeTiles.PowerupScore2X:
                LevelScore.UpdateMultiplier(2, data.Duration);
                LevelScore.UpdateScore(MatchThreeTiles.PowerupScore, MatchThreeTiles.ScorePowerup);
                break;

            case MatchThreeTiles.PowerupScore5X:
                LevelScore.UpdateMultiplier(5, data.Duration);
                LevelScore.UpdateScore(MatchThreeTiles.PowerupScore, MatchThreeTiles.ScorePowerup);
                break;

            case MatchThreeTiles.PowerupFreeze:
                ChangeTimerDuration(data.Duration);
                _game.SendPowerupFreeze(data.Duration);
                LevelScore.UpdateScore(MatchThreeTiles.PowerupScore, MatchThreeTiles.ScorePowerup);
                break;
        }
    }

    public bool CheckCollectableQuota(bool successOnly) => CheckQuota(CollectableSpawnData, _collectableCollected, successOnly);

    public bool CheckSelectableQuota(bool successOnly) => CheckQuota(SelectableSpawnData, _selectableCollected, successOnly);

    /// <summary>
    /// Every quota met. Without quotas that's a success when the level ends, but never a reason to end it.
    /// </summary>
    private static bool CheckQuota(SortedDictionary<int, MatchThreeSpawnData> spawnData, Dictionary<int, int> collected, bool successOnly)
    {
        var quotas = 0;
        var met = 0;

        foreach (var (type, data) in spawnData)
        {
            if (data.Quota <= 0)
                continue;

            quotas++;

            if (data.Quota <= collected.GetValueOrDefault(type))
                met++;
        }

        return met == quotas && (quotas > 0 || successOnly);
    }

    /// <summary>
    /// The bins: selectables and collectables with a quota or a reward count, then every bonus. The movie numbers its
    /// bins in this order. The movie's server walks its arrays in an unspecified order; here it's by type.
    /// </summary>
    private void QuotasInit()
    {
        var index = 0;
        var total = 0;

        foreach (var data in SelectableSpawnData.Values.Concat(CollectableSpawnData.Values))
        {
            if (data.Quota == 0 && data.RequiredPerReward <= 0)
                continue;

            total += data.Quota;
            _game.SendQuotaInit(index, data.Value, data.Quota, data.RequiredPerReward, data.IconId);
            data.Index = index++;
        }

        foreach (var data in BonusSpawnData.Values)
        {
            total += data.Quota;
            _game.SendQuotaInit(index, data.Value, data.Quota, data.RequiredPerReward, data.IconId);
            data.Index = index++;
        }

        _game.SendMeterInit(MoldId, total);
    }

    /// <summary>
    /// The bins this level shows: what <see cref="QuotasInit"/> sends.
    /// </summary>
    public IEnumerable<MatchThreeSpawnData> Bins =>
        SelectableSpawnData.Values.Concat(CollectableSpawnData.Values).Where(x => x.Quota != 0 || x.RequiredPerReward > 0)
            .Concat(BonusSpawnData.Values);

    #endregion

    #region Messages from the movie

    public void OnLevelEventMsg(int type, int argA, int argB)
    {
        if (Grid is null)
            return;

        switch (type)
        {
            case MatchThreeEvents.ClientFall:
                if (!Grid.FallOffBottom(argA, argB))
                    _game.StopGame($"FallOffBottomFail ({argA},{argB})");
                break;

            case MatchThreeEvents.ClientConvert:
                if (!Grid.Convert(argA, argB))
                    _game.StopGame($"ConvertFail ({argA},{argB})");
                break;

            case MatchThreeEvents.ClientTileQueueAccept:
                Grid.TileQueueAccept();
                break;

            case MatchThreeEvents.ClientTileQueueReject:
                Grid.TileQueueReject();
                break;
        }
    }

    public void OnVerifyMsg(string text)
    {
        if (Grid is not null && !Grid.CheckGrid(text.Split(',')))
            _game.StopGame("VerifyGridFail");
    }

    public void OnChainMsg(string text)
    {
        if (_game.IsDone || Grid is null)
            return;

        if (!Grid.ValidateAndRemoveChain(SplitList(text)))
            _game.StopGame($"ChainFail ({text})");
    }

    /// <summary>
    /// A pressed powerup (or loot drop): its value and the cells it touched, as the movie's own run of it chose them.
    /// </summary>
    public void OnActionMsg(int index, string text)
    {
        if (_game.IsDone || Grid is null)
            return;

        var list = SplitList(text);
        var power = PowerupSpawnData.TryGetValue(index, out var data) ? data.Power : 0;
        var success = true;

        switch (index)
        {
            case MatchThreeTiles.PowerupDynamite:
                success = Grid.StartShatter(list, power);
                break;

            case MatchThreeTiles.PowerupWildTile:
                success = Grid.StartWildTiles(list, true, power);
                break;

            case MatchThreeTiles.PowerupDestruct:
                success = Grid.StartDestruction(list, true, power);
                break;

            case MatchThreeTiles.PowerupShuffle:
                success = Grid.StartShuffle(list, true, power);
                break;

            case MatchThreeTiles.LootDropCommon:
            case MatchThreeTiles.LootDropSpecial:
            case MatchThreeTiles.LootDropRare:
                success = Grid.StartLootDrop();
                break;
        }

        if (!success)
            _game.StopGame($"ActionMsgFailure ({index}: {text})");
    }

    public void OnIdleMsg(string text)
    {
        if (_game.IsDone || Grid is null)
            return;

        Grid.NowIdle(SplitList(text));
    }

    /// <summary>
    /// The movie's lists of cells end with a comma, so the last element (empty) is dropped, as the movie's server does.
    /// </summary>
    private static List<string> SplitList(string text)
    {
        var list = text.Split(',').ToList();

        if (list.Count > 0)
            list.RemoveAt(list.Count - 1);

        return list;
    }

    #endregion
}
