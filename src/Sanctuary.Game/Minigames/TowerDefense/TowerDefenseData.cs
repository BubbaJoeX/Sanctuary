using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Sanctuary.Game.Minigames.TowerDefense;

/// <summary>
/// The tower defense server data: the six caret-separated tables the movie's own test server reads (its
/// ConfigServer names them, and GameServer.OnGameData through OnEnemyData parse them). In Free Realms the server
/// read them and sent the movie its rows; they never shipped with the client, so these are authored
/// (Resources/Minigames/TowerDefense). Lines are split the way the movie splits them, a '#' line is a header,
/// and the '^' that ends each row of a Free Realms table is dropped.
/// </summary>
public sealed class TowerDefenseData
{
    public const string GameDataFile = "TowerDefenseGameData.txt";
    public const string MapDataFile = "TowerDefenseMapData.txt";
    public const string TowerDataFile = "TowerDefenseTowerData.txt";
    public const string EnemyDataFile = "TowerDefenseEnemyData.txt";
    public const string MapTowersFile = "TowerDefenseMapTowers.txt";
    public const string MapEnemiesFile = "TowerDefenseMapEnemies.txt";

    /// <summary>
    /// GameServer's columns: the game id, and the map's game, its world (TYPE) and the rest the movie reads.
    /// </summary>
    public const int GameId = 0;

    public const int MapId = 0;
    public const int MapGameId = 1;
    public const int MapType = 7;
    public const int MapLevel = 8;
    public const int MapNumPaths = 9;
    public const int MapLives = 11;
    public const int MapStages = 12;
    public const int MapEnemyHpMultiplier = 28;

    /// <summary>
    /// MapTowers and MapEnemies rows: a map id and a tower type (or an enemy id).
    /// </summary>
    public const int LinkMapId = 0;
    public const int LinkId = 1;

    public const int TowerTowerId = 1;
    public const int TowerRank = 2;

    public const int EnemyId = 0;

    /// <summary>
    /// Columns 3 to 6 of a map row are the server's own (the game's name, description, icon and difficulty here);
    /// the movie's row (Game.OnMapDataMsg) goes from the sequence straight to the world.
    /// </summary>
    private const int MapServerColumnsStart = 3;
    private const int MapServerColumnsCount = 4;

    public List<string[]> Games { get; } = [];
    public List<string[]> Maps { get; } = [];
    public List<string[]> Towers { get; } = [];
    public List<string[]> Enemies { get; } = [];
    public List<string[]> MapTowers { get; } = [];
    public List<string[]> MapEnemies { get; } = [];

    /// <summary>
    /// Reads the tables in a folder. A missing table reads as empty, so nothing resolves.
    /// </summary>
    public static TowerDefenseData Load(string directory)
    {
        var data = new TowerDefenseData();

        data.Games.AddRange(ReadTable(Path.Combine(directory, GameDataFile)));
        data.Maps.AddRange(ReadTable(Path.Combine(directory, MapDataFile)));
        data.Towers.AddRange(ReadTable(Path.Combine(directory, TowerDataFile)));
        data.Enemies.AddRange(ReadTable(Path.Combine(directory, EnemyDataFile)));
        data.MapTowers.AddRange(ReadTable(Path.Combine(directory, MapTowersFile)));
        data.MapEnemies.AddRange(ReadTable(Path.Combine(directory, MapEnemiesFile)));

        return data;
    }

    /// <summary>
    /// What a game plays, found the way the movie's GameServer finds it: the game's row, the first map of that
    /// game, the map's tower types and enemies, then their rows. Null when any of it is missing.
    /// </summary>
    public TowerDefenseSetup? Resolve(int gameId)
    {
        if (!Games.Any(x => Int(x, GameId) == gameId))
            return null;

        var map = Maps.FirstOrDefault(x => Int(x, MapGameId) == gameId);

        if (map is null || map.Length <= MapEnemyHpMultiplier)
            return null;

        var mapId = Int(map, MapId);

        var towerTypes = MapTowers.Where(x => Int(x, LinkMapId) == mapId).Select(x => Int(x, LinkId)).Distinct().ToList();
        var enemyIds = MapEnemies.Where(x => Int(x, LinkMapId) == mapId).Select(x => Int(x, LinkId)).Distinct().ToList();

        // GameServer keeps the tower table's order, but the Free Realms movie builds its tower buttons in the order
        // the rows arrive (Game.OnTowerDataMsg), so they go out in the map's order, each tower's ranks in turn.
        var towers = towerTypes
            .SelectMany(type => Towers.Where(x => Int(x, TowerTowerId) == type).OrderBy(x => Int(x, TowerRank)))
            .ToList();

        // The movie's waves cycle through its enemies in the order they arrive (TD_Game.getEnemyType).
        var enemies = enemyIds
            .Select(id => Enemies.FirstOrDefault(x => Int(x, EnemyId) == id))
            .OfType<string[]>()
            .ToList();

        if (towers.Count == 0 || enemies.Count == 0)
            return null;

        return new TowerDefenseSetup(gameId, map, towerTypes, towers, enemies);
    }

    /// <summary>
    /// The movie's row for a map (Game.OnMapDataMsg): the server's row without its own columns.
    /// </summary>
    public static string[] ToClientMapRow(string[] map)
    {
        return map.Take(MapServerColumnsStart).Concat(map.Skip(MapServerColumnsStart + MapServerColumnsCount)).ToArray();
    }

    /// <summary>
    /// A column as the movie's parseInt reads it (a number's whole part), or 0.
    /// </summary>
    public static int Int(string[] row, int column)
    {
        if (column >= row.Length)
            return 0;

        if (int.TryParse(row[column], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return value;

        return double.TryParse(row[column], NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && number is >= int.MinValue and <= int.MaxValue ? (int)number : 0;
    }

    public static double Number(string[] row, int column)
    {
        return column < row.Length && double.TryParse(row[column], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static IEnumerable<string[]> ReadTable(string filePath)
    {
        if (!File.Exists(filePath))
            yield break;

        foreach (var rawLine in File.ReadLines(filePath))
        {
            // SoeCommon.ChopLastChar: a row may end with the carriage return of a Windows line.
            var line = rawLine.TrimEnd('\r');

            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var fields = line.Split('^');

            if (fields.Length > 1 && fields[^1].Length == 0)
                fields = fields[..^1];

            yield return fields;
        }
    }
}

/// <summary>
/// One game's map, towers and enemies, as <see cref="TowerDefenseData.Resolve"/> found them.
/// </summary>
public sealed class TowerDefenseSetup(int gameId, string[] map, IReadOnlyList<int> towerTypes, IReadOnlyList<string[]> towers,
    IReadOnlyList<string[]> enemies)
{
    public int GameId { get; } = gameId;

    /// <summary>
    /// The map's row as the server's table has it (<see cref="TowerDefenseData"/>'s Map* columns).
    /// </summary>
    public string[] Map { get; } = map;

    /// <summary>
    /// The tower types the map offers, in the order of its buttons (and of its number keys).
    /// </summary>
    public IReadOnlyList<int> TowerTypes { get; } = towerTypes;

    public IReadOnlyList<string[]> Towers { get; } = towers;

    public IReadOnlyList<string[]> Enemies { get; } = enemies;

    public int MapId => TowerDefenseData.Int(Map, TowerDefenseData.MapId);

    /// <summary>
    /// The world: 1 Penguin Wars, 2 Briarwood, 3 Garden, 4 Pixies vs Bixies (the movie's Enemies.PENGUINWARS ...).
    /// </summary>
    public int World => TowerDefenseData.Int(Map, TowerDefenseData.MapType);

    /// <summary>
    /// The world's map, from 1 (the movie loads Maps/BG_W&lt;world&gt;M&lt;level&gt;).
    /// </summary>
    public int Level => TowerDefenseData.Int(Map, TowerDefenseData.MapLevel);

    public int NumPaths => TowerDefenseData.Int(Map, TowerDefenseData.MapNumPaths);

    public int Lives => TowerDefenseData.Int(Map, TowerDefenseData.MapLives);

    public int Stages => TowerDefenseData.Int(Map, TowerDefenseData.MapStages);

    public string MapRow => string.Join('^', TowerDefenseData.ToClientMapRow(Map));

    public IEnumerable<string> TowerRows => Towers.Select(x => string.Join('^', x));

    public IEnumerable<string> EnemyRows => Enemies.Select(x => string.Join('^', x));
}
