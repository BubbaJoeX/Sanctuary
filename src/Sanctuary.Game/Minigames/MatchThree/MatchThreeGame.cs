using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Minigames.Soe;

namespace Sanctuary.Game.Minigames.MatchThree;

/// <summary>
/// The match-three games: Mining, Forging, Mail Sorting, Harvesting, Artifact Hunt and Goo'd Candy (game_mining,
/// game_forging, game_mailsorting, game_farming, game_artifact and game_halloween). The six movies share one
/// engine and one server (their GameServer, core.ServerGrid, GameLevel, ...), ported here; they differ only in their
/// art. Each game plays levels on a board of tiles: chains of three or more selectables clear them, collectables
/// that reach a drop zone at the bottom are collected, and a level succeeds when its quotas (the bins) are met
/// before its time runs out. A chain adds time.
/// <para>
/// The movie never learns which game it plays: its client half only asks for one in its local test mode
/// (ConfigServer.GAME_ID), and in Free Realms it never sends FRServer_RequestStageId. The server picks the rows of
/// the data files by the game's <see cref="Resources.Definitions.MinigameDefinition.Level"/> (the GAME column), as
/// the real server did with its own copy of these tables, which never shipped with the client. They're authored
/// under Resources/Minigames/MatchThree in the formats the movie's server reads.
/// </para>
/// <para>
/// The flow: connect (answered once the data is loaded with OnServerReadyMsg); the movie shows its tutorial and
/// starts the game; each level is announced (OnNextLevelMsg), the movie builds it and starts it (OnStartLevelMsg),
/// the server sends the tiles, the board and the bins and the movie plays; the server ends the level when its time
/// is up or its quotas are met (OnStopLevel). After a success the movie asks for the next level; past the last
/// one the game is won. A failed level loses the game (the movie plays its failure and closes itself).
/// </para>
/// </summary>
[FlashMinigame("MatchThree")]
public sealed class MatchThreeGame : SoeGameServer
{
    public const string GameDataFile = "MatchThreeGameData.txt";
    public const string LevelDataFile = "MatchThreeLevelData.txt";
    public const string SpawnDataFile = "MatchThreeSpawnData.txt";
    public const string LevelSpawnMapFile = "MatchThreeLevelSpawns.txt";
    public const string GridDataFile = "MatchThreeGridData.txt";
    public const string TimerDataFile = "MatchThreeTimerData.txt";
    public const string ProgressionDataFile = "MatchThreeProgression.txt";

    // MatchThreeProgression.txt
    private const int PdLevel = 1;
    private const int PdProperty = 2;
    private const int PdInit = 3;
    private const int PdDelta = 4;
    private const int PdMax = 5;

    // MatchThreeSpawnData.txt
    private const int SdId = 0;
    private const int SdCategory = 1;
    private const int SdType = 2;
    private const int SdSpawnChance = 3;
    private const int SdCreateMaximum = 4;
    private const int SdCollectMaximum = 5;
    private const int SdQuota = 6;
    private const int SdRewardSet = 7;
    private const int SdIconId = 8;
    private const int SdDuration = 9;
    private const int SdPower = 10;
    private const int SdRequiredPerReward = 11;
    private const int SdCollectionType = 12;
    private const int SdTimerId = 13;

    // MatchThreeLevelData.txt
    private const int LdId = 0;
    private const int LdGame = 1;
    private const int LdIndex = 2;
    private const int LdDuration = 3;
    private const int LdObjectiveId = 4;
    private const int LdGridId = 7;
    private const int LdCollectableSpawn = 8;
    private const int LdBonusSpawn = 9;
    private const int LdSelectableSpawn = 10;
    private const int LdPowerupSpawn = 11;
    private const int LdHazardSpawn = 12;
    private const int LdBlockerSpawn = 13;
    private const int LdWildcardSpawn = 14;
    private const int LdType = 16;
    private const int LdTypeData = 17;

    // MatchThreeTimerData.txt
    private const int TdId = 0;
    private const int TdType = 1;
    private const int TdChainM = 2;
    private const int TdChainF = 3;
    private const int TdChainB = 4;
    private const int TdPerTile = 5;

    private readonly Dictionary<int, MatchThreeSpawnData> _rawSpawn = [];
    private readonly Dictionary<int, MatchThreeTimerData> _rawTimers = [];
    private readonly Dictionary<int, MatchThreeLevel> _rawLevels = [];
    private readonly List<int[][]> _gridTemplates = [];

    private bool _dataLoaded;
    private int _totalScore;

    /// <summary>
    /// The game's rows in the data files (the GAME column): its definition's Level.
    /// </summary>
    public int GameId { get; private set; }

    /// <summary>
    /// MatchThreeGameData.txt's duration and score target. The movie's server reads them but plays by its levels.
    /// </summary>
    public int MasterDuration { get; private set; }
    public int ScoreTarget { get; private set; }

    /// <summary>
    /// What makes the tiles and the shared random numbers; tests seed it.
    /// </summary>
    public Random Random { get; set; } = Random.Shared;

    public IReadOnlyDictionary<int, MatchThreeTimerData> Timers => _rawTimers;

    /// <summary>
    /// The level being played, if any.
    /// </summary>
    public MatchThreeLevel? PlayingLevel => CurrentLevel as MatchThreeLevel;

    public IEnumerable<MatchThreeLevel> Levels => Enumerable.Range(1, LevelCount).Select(GetLevel).OfType<MatchThreeLevel>();

    #region Game (GameServer)

    protected override void GameInitialize()
    {
        // The movie's CleanupScene also tells the movie it's inactive; the movie isn't loaded yet, and it hears so
        // when it connects.
        Reset();
        SetActive(false);

        SetGameId(Definition.Level);
    }

    protected override bool IsGameReady() => _dataLoaded;

    protected override bool GameReset()
    {
        _totalScore = 0;
        return true;
    }

    protected override void GameClientConnect()
    {
        SendActive(false);
    }

    protected override void GameApplyTick(long delta)
    {
        if (!IsInProgress || IsPaused || PlayingLevel is not { } level)
            return;

        level.ApplyTick(delta);

        if (!level.IsDone)
            CheckDone();
    }

    private void CheckDone()
    {
        if (IsInProgress && PlayingLevel is { } level && level.IsLevelDone())
            StopLevel();
    }

    protected override void OnStartGame()
    {
        NextLevel();
    }

    protected override void OnNextLevel()
    {
        var level = PlayingLevel!;
        level.Level = Level;

        SendNextLevel(Level, level.Goal, level.ScoreBest);
    }

    protected override void OnStartLevel()
    {
        Score = _totalScore;

        SetActive(true);
        SendActive(true);
    }

    /// <summary>
    /// Tells the movie whether the level succeeded. A failure is the end: the movie plays its failure and ends the
    /// game itself (it never asks for the next level), so the server records the loss here. It doesn't send the
    /// loss (OnGameLostMsg), which would cut the movie's failure short.
    /// <para>
    /// The movie's own server also sent OnActiveMsg false here (its DeactivateEverything), but the movie takes that
    /// as a pause and stops ticking: it never counts down to asking for the next level, nor to ending a failed game,
    /// and freezes on the finished board. So only the server stops taking moves.
    /// </para>
    /// </summary>
    protected override void OnStopLevel()
    {
        SetActive(false);

        var success = PlayingLevel?.CheckSuccess() ?? false;
        Send("OnStopLevel", Level, success);

        if (!success)
            GameLost();
    }

    protected override void OnFinishLevel()
    {
        NextLevel();
    }

    /// <summary>
    /// The movie asks for the next level once its success is shown. A lost or stopped game has no next level.
    /// </summary>
    protected override void OnNextLevelMsg()
    {
        if (!IsInProgress)
            return;

        base.OnNextLevelMsg();
    }

    protected override void OnStopGame(string reason)
    {
        Logger.LogWarning("{player} stopped minigame {id} ({name}): {reason}.", Player?.Name.FullName, Definition.Id,
            Definition.Comment, reason);

        SendGameStopped();
    }

    protected override void OnLost()
    {
    }

    protected override bool OnGameMessage(string name, FlashArgs args)
    {
        switch (name)
        {
            case "OnChainMsg":
                PlayingLevel?.OnChainMsg(args.String(0));
                return true;

            case "OnActionMsg":
                PlayingLevel?.OnActionMsg(args.Int(0), args.String(1));
                return true;

            case "OnIdleMsg":
                PlayingLevel?.OnIdleMsg(args.String(0));
                return true;

            case "OnVerifyMsg":
                PlayingLevel?.OnVerifyMsg(args.String(0));
                return true;

            case "OnLevelEventMsg":
                PlayingLevel?.OnLevelEventMsg(args.Int(0), args.Int(1), args.Int(2));
                return true;

            case "OnDebugMsg":
                // The movie's test harness can skip a level (SkipLevel), which would win it; the shipped movie never
                // sends it, so it isn't honoured.
                Logger.LogInformation("{player} sent minigame {id} a debug command ({command}), which is ignored.",
                    Player?.Name.FullName, Definition.Id, args.String(0));
                return true;

            case "FRServer_GameWon":
            case "FRServer_GameLost":
            case "FRServer_GameOver":
            case "FRServer_GameClose":
            case "FRServer_GameQuit":
                // MinigameManager closes the movie; the server decided the result already.
                return true;
        }

        return false;
    }

    /// <summary>
    /// The player's running score across the levels (each level's own score goes to the movie as it changes).
    /// </summary>
    internal void AddScore(int delta)
    {
        _totalScore += delta;
        Score = _totalScore;
    }

    internal void LogWarning(string message)
    {
        Logger.LogWarning("Minigame {id} ({name}): {message}.", Definition.Id, Definition.Comment, message);
    }

    #endregion

    #region Data (GameServer's LoadData chain)

    /// <summary>
    /// Loads the game's rows. The movie's server loads its files one after another, each when the last is in:
    /// spawns, grids, timers, the game, its levels, the level spawns and the progression; then it's ready.
    /// </summary>
    private void SetGameId(int id)
    {
        GameId = id;

        OnSpawnData(ReadData(SpawnDataFile));
        OnGridData(ReadData(GridDataFile));
        OnTimerData(ReadData(TimerDataFile));
        OnGameData(ReadData(GameDataFile));

        if (LevelCount == 0)
        {
            Logger.LogError("Minigame {id} ({name}) has no match-three levels for game {game} in {directory}.",
                Definition.Id, Definition.Comment, GameId, DataDirectory);
            return;
        }

        _dataLoaded = true;
        SetReady();
    }

    private string ReadData(string fileName)
    {
        var path = Path.Combine(DataDirectory, fileName);

        if (File.Exists(path))
            return File.ReadAllText(path);

        Logger.LogError("Minigame {id} ({name}) is missing its data file {path}.", Definition.Id, Definition.Comment, path);
        return string.Empty;
    }

    private void OnGameData(string text)
    {
        foreach (var line in Lines(text))
        {
            var fields = line.Split('^');

            if (ParseInt(fields, 0) != GameId)
                continue;

            MasterDuration = ParseInt(fields, 1) ?? 0;
            ScoreTarget = ParseInt(fields, 3) ?? 0;

            OnLevelData(ReadData(LevelDataFile));
            break;
        }

        OnSpawnMapData(ReadData(LevelSpawnMapFile));
    }

    private void OnSpawnMapData(string text)
    {
        foreach (var line in Lines(text))
        {
            var fields = line.Split('^');

            if (ParseInt(fields, 0) is not { } levelId || !_rawLevels.TryGetValue(levelId, out var level))
                continue;

            if (ParseInt(fields, 1) is not { } spawnId || !_rawSpawn.TryGetValue(spawnId, out var data))
                continue;

            switch (data.Category)
            {
                case MatchThreeSpawnData.CategoryCollectable:
                    level.AddCollectableSpawnData(data);
                    break;

                case MatchThreeSpawnData.CategoryBonus:
                    level.AddBonusSpawnData(data);
                    break;

                case MatchThreeSpawnData.CategorySelectable:
                    level.AddSelectableSpawnData(data);
                    break;

                case MatchThreeSpawnData.CategoryPowerup:
                    level.AddPowerupSpawnData(data);
                    break;

                case MatchThreeSpawnData.CategoryLootDrop:
                    level.AddLootDropSpawnData(data);
                    break;
            }
        }

        OnProgressionData(ReadData(ProgressionDataFile));
    }

    private void OnProgressionData(string text)
    {
        foreach (var line in Lines(text))
        {
            var fields = line.Split('^');

            if (ParseInt(fields, PdLevel) is not { } levelId || !_rawLevels.TryGetValue(levelId, out var level))
                continue;

            level.AddProgressionData(ParseInt(fields, PdProperty) ?? 0, ParseInt(fields, PdInit) ?? 0,
                ParseInt(fields, PdDelta) ?? 0, ParseInt(fields, PdMax) ?? 0);
        }
    }

    private void OnSpawnData(string text)
    {
        foreach (var line in Lines(text))
        {
            var fields = line.Split('^');

            if (ParseInt(fields, SdId) is not { } id)
                continue;

            var data = new MatchThreeSpawnData(id);

            // As the movie's setters: a blank number keeps the default.
            data.Category = ParseInt(fields, SdCategory) ?? data.Category;
            data.Type = ParseInt(fields, SdType) ?? data.Type;
            data.SpawnChance = ParseInt(fields, SdSpawnChance) ?? data.SpawnChance;
            data.Quota = ParseInt(fields, SdQuota) ?? data.Quota;
            data.MaximumToCreate = ParseInt(fields, SdCreateMaximum) ?? data.MaximumToCreate;
            data.MaximumToCollect = ParseInt(fields, SdCollectMaximum) ?? data.MaximumToCollect;
            data.RewardSetId = ParseInt(fields, SdRewardSet) ?? data.RewardSetId;
            data.IconId = ParseInt(fields, SdIconId) ?? 0;
            data.Duration = ParseInt(fields, SdDuration) ?? 0;
            data.Power = ParseInt(fields, SdPower) ?? 0;
            data.RequiredPerReward = ParseInt(fields, SdRequiredPerReward) ?? data.RequiredPerReward;
            data.CollectionType = ParseInt(fields, SdCollectionType) ?? 0;
            data.TimerId = ParseInt(fields, SdTimerId) ?? 0;

            _rawSpawn[id] = data;
        }
    }

    private void OnLevelData(string text)
    {
        foreach (var line in Lines(text))
        {
            var fields = line.Split('^');

            if (ParseInt(fields, LdGame) != GameId)
                continue;

            if (ParseInt(fields, LdId) is not { } levelId || ParseInt(fields, LdIndex) is not { } index)
                continue;

            var level = new MatchThreeLevel(this)
            {
                LevelId = levelId,
                Index = index,
                ObjectiveId = ParseInt(fields, LdObjectiveId) ?? 0,
                CollectableSpawnChance = ParseInt(fields, LdCollectableSpawn),
                BonusSpawnChance = ParseInt(fields, LdBonusSpawn),
                SelectableSpawnChance = ParseInt(fields, LdSelectableSpawn),
                PowerupSpawnChance = ParseInt(fields, LdPowerupSpawn),
                HazardSpawnChance = ParseInt(fields, LdHazardSpawn),
                BlockerSpawnChance = ParseInt(fields, LdBlockerSpawn),
                WildcardSpawnChance = ParseInt(fields, LdWildcardSpawn),
                Type = (MatchThreeLevelType)(ParseInt(fields, LdType) ?? 0),
                MoldId = ParseInt(fields, LdTypeData) ?? 0,
                GridId = ParseInt(fields, LdGridId) ?? 0
            };

            level.SetEndTime(ParseInt(fields, LdDuration) ?? 0);

            var stopAtQuota = level.Type.HasFlag(MatchThreeLevelType.StopAtQuota);
            level.CollectableStopAtQuota = stopAtQuota;
            level.SelectableStopAtQuota = stopAtQuota;

            if (level.GridId < 0 || level.GridId >= _gridTemplates.Count)
            {
                Logger.LogError("Match-three level {level} (game {game}) uses grid {grid}, which {file} doesn't have.",
                    levelId, GameId, level.GridId, GridDataFile);
                continue;
            }

            level.GridShape = _gridTemplates[level.GridId];

            AddLevel(index, level);
            _rawLevels[levelId] = level;
        }
    }

    /// <summary>
    /// Grids are a "grid^N" line (N counting from 0, in file order) and then their rows, each starting with a digit;
    /// the line after the last row is skipped, so grids need a line between them. Lines starting with # are notes.
    /// </summary>
    private void OnGridData(string text)
    {
        var lines = text.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith('#'))
                continue;

            var header = lines[i].Split('^');

            if (!header[0].Trim().Equals("grid", StringComparison.OrdinalIgnoreCase) || ParseInt(header, 1) != _gridTemplates.Count)
                continue;

            var rows = new List<int[]>();
            var row = i + 1;

            while (row < lines.Length && lines[row].Length > 0 && char.IsAsciiDigit(lines[row][0]))
            {
                rows.Add(lines[row].Split('^').Select(cell => MatchThreeGrid.TryParseInt(cell, out var value) ? value : 0).ToArray());
                row++;
            }

            if (rows.Count == 0)
                continue;

            // The movie reads the board as rows of the first row's width.
            var width = rows[0].Length;
            _gridTemplates.Add(rows.Select(x => x.Length == width ? x : [.. x.Take(width), .. Enumerable.Repeat(0, Math.Max(0, width - x.Length))]).ToArray());
            i = row;
        }
    }

    private void OnTimerData(string text)
    {
        foreach (var line in Lines(text))
        {
            var fields = line.Split('^');

            if (ParseInt(fields, TdId) is not { } id)
                continue;

            _rawTimers[id] = new MatchThreeTimerData(id, ParseInt(fields, TdType) ?? 0, (ParseInt(fields, TdChainM) ?? 0) / 10.0,
                (ParseInt(fields, TdChainF) ?? 0) / 10.0, (ParseInt(fields, TdChainB) ?? 0) / 10.0, ParseInt(fields, TdPerTile) ?? 0);
        }
    }

    /// <summary>
    /// The lines of a data file, without their carriage returns (the movie's ChopLastChar).
    /// </summary>
    private static IEnumerable<string> Lines(string text)
    {
        return text.Split('\n').Select(x => x.TrimEnd('\r'));
    }

    private static int? ParseInt(string[] fields, int index)
    {
        return index < fields.Length && MatchThreeGrid.TryParseInt(fields[index], out var value) ? value : null;
    }

    #endregion

    #region Messages to the movie (GameServerNetwork)

    internal void SendLevelInit(int level, double duration, int type) => Send("OnLevelInitMsg", level, duration, type);

    internal void SendTileQueue(int reset, IEnumerable<int> list) => Send("OnTileQueueMsg", reset, list);

    internal void SendRandomNumberQueue(IEnumerable<int> list) => Send("OnRandomQueueMsg", list);

    internal void SendBuildMap(int width, int height, IEnumerable<int> list) => Send("OnBuildMapMsg", width, height, list);

    internal void SendTileInfo(int value, int type) => Send("OnTileInfoMsg", value, type);

    internal void SendScoreIncrement(int delta, int reason) => Send("OnLevelEventMsg", MatchThreeEvents.ScoreIncrement, delta, reason);

    internal void SendScoreMultiplier(int value, int duration) => Send("OnLevelEventMsg", MatchThreeEvents.ScoreMultiplier, value, duration);

    internal void SendScoreMultiplierExpired() => Send("OnLevelEventMsg", MatchThreeEvents.ScoreMultiplierExpired);

    internal void SendTimerIncrement(double delta, int reason) => Send("OnLevelEventMsg", MatchThreeEvents.TimerIncrement, delta, reason);

    internal void SendPowerupInit(int type, int duration, int power) => Send("OnPowerupInitMsg", type, duration, power);

    internal void SendQuotaInit(int index, int value, int quota, int perCluster, int iconId) =>
        Send("OnQuotaInitMsg", index, value, quota, perCluster, iconId);

    internal void SendQuotaUpdate(int index, int collected, int quota, int perCluster) =>
        Send("OnQuotaUpdateMsg", index, collected, quota, perCluster);

    internal void SendMeterInit(int type, int quota) => Send("OnLevelEventMsg", MatchThreeEvents.MeterInit, type, quota);

    internal void SendPowerupFreeze(int duration) => Send("OnLevelEventMsg", MatchThreeEvents.PowerupFreeze, duration);

    internal void SendNextWave(int wave) => Send("OnLevelEventMsg", MatchThreeEvents.LevelNextWave, wave);

    internal void SendLevelReshuffle(int reason) => Send("OnLevelEventMsg", MatchThreeEvents.LevelReshuffle, reason);

    #endregion
}
