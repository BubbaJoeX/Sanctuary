using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Sanctuary.Game.Minigames.MatchThree;

/// <summary>
/// The server's board (the movies' core.ServerGrid). The movie runs the same board itself and the two stay in step
/// by sharing two queues the server fills: the tiles that drop in (OnTileQueueMsg) and the random numbers the
/// powerups pick tiles with (OnRandomQueueMsg). The movie tells the server what the player did (a chain, a powerup,
/// a tile falling into a drop zone, tiles coming to rest) and the server replays it here, checking it, scoring it and
/// counting what was collected. A move the board can't have made stops the game.
/// <para>
/// The board has an extra row under the template (the movie's grid arrays are one row longer), and the falls never
/// touch row 0 or column 0, so templates keep those empty.
/// </para>
/// </summary>
public sealed class MatchThreeGrid
{
    public const int TileQueueBatchSize = 20;
    public const int TileQueueMinLength = 50;
    public const int RandomQueueBatchSize = 20;
    public const int RandomQueueMinLength = 50;
    public const int RandomMaxValue = 999;

    /// <summary>
    /// Settling the board recurses until nothing falls; this is far past what a real board needs.
    /// </summary>
    private const int MaximumUpdateDepth = 64;

    /// <summary>
    /// A level whose tiles can never make a move would rebuild its board forever.
    /// </summary>
    private const int MaximumReshuffles = 20;

    private readonly MatchThreeLevel _level;
    private readonly MatchThreeGame _game;
    private readonly MatchThreeTileGenerator _generator;
    private readonly Random _random;
    private readonly int[][] _shape;

    /// <summary>
    /// Every tile made this level, in the order the movie made them too: the powerups pick from it by index.
    /// Tiles that are gone stay (destroyed) until a reshuffle; only live ones are on the board.
    /// </summary>
    private readonly List<MatchThreeTile> _tiles = [];

    private readonly Queue<int> _randomNumbers = new();
    private List<int> _unsentRandomNumbers = [];

    private List<MatchThreeTile> _chain = [];
    private List<MatchThreeTile> _randomTiles = [];
    private List<MatchThreeTile> _shatter1 = [];
    private List<MatchThreeTile> _shatter2 = [];

    private Queue<MatchThreeTile> _tileQueue = new();
    private List<MatchThreeTile> _tilePendingQueue = [];
    private List<int> _tileUnsentValues = [];

    private MatchThreeTile?[,] _current;

    private bool _hasFall;
    private bool _hasExploded;

    private int _updateDepth;
    private int _reshuffleDepth;

    public MatchThreeGrid(MatchThreeLevel level, MatchThreeTileGenerator generator, int[][] shape, MatchThreeGame game)
    {
        _level = level;
        _game = game;
        _generator = generator;
        _random = game.Random;
        _shape = shape;

        Height = shape.Length;
        Width = shape.Length > 0 ? shape[0].Length : 0;

        _current = new MatchThreeTile?[Height + 1, Width];

        BuildMap();
        GenerateTilesForQueue(TileQueueMinLength + TileQueueBatchSize, MatchThreeTiles.Any, MatchThreeTiles.Any);
        SendTileQueue(false);
        GenerateRandomNumbersForQueue(RandomQueueMinLength + RandomQueueBatchSize);
        SendRandomNumberQueue();
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>
    /// Tiles queued to drop in, as the movie has them.
    /// </summary>
    public int TileQueueLength => _tileQueue.Count;

    /// <summary>
    /// The live tile at a cell, or null.
    /// </summary>
    public MatchThreeTile? TileAt(int x, int y) => y >= 0 && y <= Height && x >= 0 && x < Width ? _current[y, x] : null;

    public IEnumerable<MatchThreeTile> LiveTiles => _tiles.Where(x => x.State != MatchThreeTileState.Destroy);

    private void SetTileAt(int x, int y, MatchThreeTile? tile)
    {
        if (y >= 0 && y <= Height && x >= 0 && x < Width)
            _current[y, x] = tile;
    }

    #region Messages from the movie

    /// <summary>
    /// OnIdleMsg: tiles the movie has finished moving.
    /// </summary>
    public void NowIdle(IReadOnlyList<string> list)
    {
        for (var i = 0; i < list.Count / 2; i++)
        {
            var tile = TileAt(ParseInt(list[i * 2]), ParseInt(list[i * 2 + 1]));

            if (tile?.State is MatchThreeTileState.Pending or MatchThreeTileState.Appear)
                tile.SetState(MatchThreeTileState.Idle);
        }
    }

    /// <summary>
    /// OnChainMsg: the cells of a chain, in the order the player dragged it. It must be at least three tiles of one
    /// selectable (and wildcards), each next to the one before.
    /// </summary>
    public bool ValidateAndRemoveChain(IReadOnlyList<string> list)
    {
        _chain = [];

        var count = list.Count / 2;

        if (count < MatchThreeTiles.MinimumChainLength)
            return false;

        if (!CheckCurrentTile(ParseInt(list[0]), ParseInt(list[1])))
            return false;

        for (var i = 1; i < count; i++)
        {
            if (!CheckNextTile(ParseInt(list[i * 2]), ParseInt(list[i * 2 + 1])))
            {
                UpdateCurrentGridArray();
                return false;
            }
        }

        DoMatch3();
        return true;
    }

    /// <summary>
    /// CLIENT_FALL: a tile flagged to fall off the bottom reached the drop zone, which collects it.
    /// </summary>
    public bool FallOffBottom(int x, int y)
    {
        var tile = TileAt(x, y);

        if (tile is null || !tile.IsFallOffBottom)
            return false;

        tile.AddCollect();
        tile.SetState(MatchThreeTileState.Explode);
        _hasExploded = true;
        UpdateCurrentGridArray();

        return true;
    }

    /// <summary>
    /// CLIENT_CONVERT: a tile flagged to turn into a wildcard did.
    /// </summary>
    public bool Convert(int x, int y)
    {
        var tile = TileAt(x, y);

        if (tile is null || !tile.IsConvert)
            return false;

        tile.Convert();
        return true;
    }

    /// <summary>
    /// OnVerifyMsg: the movie's board as x,y,value triples (its debug check; off in the shipped movies).
    /// </summary>
    public bool CheckGrid(IReadOnlyList<string> list)
    {
        for (var i = 0; i < list.Count / 3; i++)
        {
            var tile = TileAt(ParseInt(list[i * 3]), ParseInt(list[i * 3 + 1]));

            if (tile is null || !TryParseInt(list[i * 3 + 2], out var clientValue))
                continue;

            var value = tile.NetworkValue;

            if (value != 0 && clientValue != 0 && value != clientValue)
                return false;
        }

        return true;
    }

    public void TileQueueAccept()
    {
        _tileQueue = new Queue<MatchThreeTile>();
        TileQueueAddPending();
    }

    /// <summary>
    /// The movie's queue didn't match (it had used tiles the server hadn't heard about yet): offer it again.
    /// </summary>
    public void TileQueueReject()
    {
        SendTileQueue(true);
    }

    #endregion

    #region Board

    /// <summary>
    /// A new wave: new chances for the tiles still to come, so the queue is replaced (the movie accepts the new one
    /// once it has used up as many as the server has, or asks again).
    /// </summary>
    public void NextWave()
    {
        _generator.SetBaseRandomizers();
        GenerateTilesForQueue(TileQueueMinLength + TileQueueBatchSize, MatchThreeTiles.Any, MatchThreeTiles.Any);
        SendTileQueue(true);
    }

    /// <summary>
    /// No moves left: a new board (the movie clears its own and its tile queue when it hears why).
    /// </summary>
    public void LevelReshuffle()
    {
        if (_reshuffleDepth >= MaximumReshuffles)
        {
            _game.LogWarning($"level {_level.LevelId} still has no moves after {MaximumReshuffles} new boards");
            return;
        }

        _reshuffleDepth++;

        GenerateGrid();

        foreach (var tile in _tiles)
            tile.SetState(MatchThreeTileState.Destroy);

        _tiles.Clear();

        _game.SendLevelReshuffle(MatchThreeTiles.ReshuffleNoMoves);
        BuildMap();
        GenerateTilesForQueue(TileQueueMinLength + TileQueueBatchSize, MatchThreeTiles.Any, MatchThreeTiles.Any);
        SendTileQueue(false);

        _reshuffleDepth--;
    }

    /// <summary>
    /// Lays the template out: 0 empty, 1 a regular tile, 2 any tile, 3 a blocker, 4 a drop zone; another value is a
    /// tile of its category (negative: exactly that value). The movie draws its board from the same tiles, taken off
    /// the tile queue cell by cell.
    /// </summary>
    private void BuildMap()
    {
        _tileQueue = new Queue<MatchThreeTile>();
        _tilePendingQueue = [];
        _tileUnsentValues = [];

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var cell = x < _shape[y].Length ? _shape[y][x] : 0;

                switch (cell)
                {
                    case 0:
                        AddMapTile(x, y, MatchThreeTiles.Empty, MatchThreeTiles.Any);
                        break;

                    case 1:
                        AddMapTile(x, y, MatchThreeTiles.BuildRegular, MatchThreeTiles.Any);
                        break;

                    case 2:
                        AddMapTile(x, y, MatchThreeTiles.BuildAny, MatchThreeTiles.Any);
                        break;

                    case 3:
                        AddMapTile(x, y, MatchThreeTiles.Blocker, MatchThreeTiles.Any);
                        break;

                    case 4:
                        AddMapTile(x, y, MatchThreeTiles.DropZone, MatchThreeTiles.Any);
                        break;

                    default:
                        if (cell < 0)
                            AddMapTile(x, y, MatchThreeTiles.CategoryOf(-cell), -cell);
                        else
                            AddMapTile(x, y, MatchThreeTiles.CategoryOf(cell), MatchThreeTiles.Any);
                        break;
                }
            }
        }

        SendTileQueue(false);
        _game.SendBuildMap(Width, Height, _shape.SelectMany(row => row));
        UpdateCurrentGridArray();
    }

    private void AddMapTile(int x, int y, int category, int value)
    {
        var tile = _generator.GenerateTile(category, value);
        tile.SetPosition(x, y);
        tile.SetGrid(this);

        _tileUnsentValues.Add(tile.NetworkValue);
        _tiles.Add(tile);
    }

    /// <summary>
    /// Whether the player has a move: a pressable powerup, or a selectable (or wildcard) with two neighbours it
    /// could chain through.
    /// </summary>
    public bool HasMove()
    {
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                var tile = TileAt(x, y);

                switch (tile?.Category)
                {
                    case MatchThreeTiles.Powerup:
                        if (tile.Value >= MatchThreeTiles.PowerupPressMe)
                            return true;
                        break;

                    case MatchThreeTiles.Wildcard:
                    case MatchThreeTiles.Selectable:
                        if (GetMatchingNeighborCount(x, y, tile.Value) > 1)
                            return true;
                        break;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// The movie's count of neighbours a tile matches; a wildcard also counts its best pair of neighbours that match
    /// each other.
    /// </summary>
    private int GetMatchingNeighborCount(int x, int y, int value)
    {
        var count = 0;
        var up = 0;
        var down = 0;
        var right = 0;
        var left = 0;

        var upValue = MatchThreeTiles.Empty;
        var downValue = MatchThreeTiles.Empty;
        var rightValue = MatchThreeTiles.Empty;

        var upTile = TileAt(x, y - 1);

        if (upTile is not null)
        {
            upValue = upTile.Value;
            count += ValueMatchesNeighbor(value, upValue);

            if (upTile.Category == MatchThreeTiles.Selectable)
                up++;
        }

        var downTile = TileAt(x, y + 1);

        if (downTile is not null)
        {
            downValue = downTile.Value;
            count += ValueMatchesNeighbor(value, downValue);

            if (downTile.Category == MatchThreeTiles.Selectable)
            {
                up += ValueMatchesNeighbor(upValue, downValue);
                down++;
            }
        }

        var rightTile = TileAt(x + 1, y);

        if (rightTile is not null)
        {
            rightValue = rightTile.Value;
            count += ValueMatchesNeighbor(value, rightValue);

            if (rightTile.Category == MatchThreeTiles.Selectable)
            {
                up += ValueMatchesNeighbor(upValue, rightValue);
                down += ValueMatchesNeighbor(downValue, rightValue);
                right++;
            }
        }

        var leftTile = TileAt(x - 1, y);

        if (leftTile is not null)
        {
            var leftValue = leftTile.Value;
            count += ValueMatchesNeighbor(value, leftValue);

            if (leftTile.Category == MatchThreeTiles.Selectable)
            {
                up += ValueMatchesNeighbor(upValue, leftValue);
                down += ValueMatchesNeighbor(downValue, leftValue);
                right += ValueMatchesNeighbor(rightValue, leftValue);
                left++;
            }
        }

        if (value == MatchThreeTiles.Wildcard)
            count += Math.Max(0, Math.Max(Math.Max(up, down), Math.Max(right, left)));

        return count;
    }

    private static int ValueMatchesNeighbor(int neighborValue, int value)
    {
        return value == MatchThreeTiles.Wildcard || neighborValue == value ? 1 : 0;
    }

    private bool CheckNextTile(int x, int y)
    {
        var last = _chain[^1];

        if (x == last.X + 1 || x == last.X - 1)
            return y == last.Y && CheckCurrentTile(x, y);

        if (y == last.Y + 1 || y == last.Y - 1)
            return x == last.X && CheckCurrentTile(x, y);

        return false;
    }

    /// <summary>
    /// Adds a cell to the chain if it can be: not already in it, and a wildcard, the chain's selectable, or (while
    /// the chain is only wildcards) any selectable.
    /// </summary>
    private bool CheckCurrentTile(int x, int y)
    {
        var tile = TileAt(x, y);

        if (tile is null || _chain.Contains(tile))
            return false;

        var chainValue = _chain.LastOrDefault(t => t.Category == MatchThreeTiles.Selectable)?.Value ?? 0;

        if (tile.Value == MatchThreeTiles.Wildcard)
        {
            _chain.Add(tile);
            return true;
        }

        if (chainValue != 0)
        {
            if (tile.Value != chainValue)
                return false;
        }
        else if (tile.Category != MatchThreeTiles.Selectable)
        {
            return false;
        }

        _chain.Add(tile);
        return true;
    }

    /// <summary>
    /// The chain explodes (and is collected), except locked tiles, which only unlock; wildcards count as the chain's
    /// selectable. Then the time bonus and the chain's score.
    /// </summary>
    private void DoMatch3()
    {
        int? value = null;
        var hasWildcard = false;

        foreach (var tile in _chain)
        {
            if (tile.Category != MatchThreeTiles.Wildcard)
                value ??= tile.Value;
            else
                hasWildcard = true;

            if (tile.IsLocked)
            {
                tile.Unlock();
            }
            else
            {
                tile.AddCollect();
                tile.SetState(MatchThreeTileState.Explode);
                _hasExploded = true;
            }
        }

        if (hasWildcard && value is not null)
        {
            foreach (var tile in _chain.Where(t => t.Category == MatchThreeTiles.Wildcard))
                tile.SetChainValue(value.Value);
        }

        _level.CalculateChainTimeBonus(_chain.Count);
        _level.CalculateChainScore(_chain.Count);

        _chain = [];
        UpdateCurrentGridArray();
    }

    /// <summary>
    /// Fills the holes column by column, right to left: a tile off the queue drops in above the column for each
    /// hole, everything above a hole falls a row, and over a drop zone the first falling kind of tile (collectables,
    /// bonuses, blockers, collected powerups) is flagged to fall off the bottom. True if anything is falling.
    /// </summary>
    private bool CheckFalls()
    {
        _hasFall = false;

        for (var x = Width - 1; x > 0; x--)
        {
            var allEmpty = true;
            var y = 0;

            for (y = 0; y < Height && allEmpty; y++)
            {
                if (TileAt(x, y)?.Category != MatchThreeTiles.Empty)
                    allEmpty = false;
            }

            var regenY = y - 2;
            var regenerated = 0;

            for (var row = Height - 1; row > 0; row--)
            {
                var tile = TileAt(x, row);

                if (tile?.Category == MatchThreeTiles.DropZone)
                {
                    for (var above = row - 1; above >= 0; above--)
                    {
                        var aboveTile = TileAt(x, above);

                        if (aboveTile is null)
                            continue;

                        if (IsInFallType(aboveTile.Value))
                            aboveTile.AddFallOffBottom();
                        else if (IsInTransformType(aboveTile.Value))
                            aboveTile.AddConvert();

                        break;
                    }
                }
                else if (tile is null)
                {
                    RegenTile(x, regenY);
                    regenerated++;
                    regenY--;
                    AddColumnFall(row, x);
                }
            }

            for (var row = y - 2; row > regenY; row--)
            {
                for (var i = 1; i <= regenerated; i++)
                {
                    _tiles[^i].AddFall();
                    _hasFall = true;
                }
            }
        }

        return _hasFall;
    }

    private static bool IsInTransformType(int value)
    {
        return value is >= MatchThreeTiles.TransformTypeStart and < MatchThreeTiles.TransformTypeEnd;
    }

    private static bool IsInFallType(int value)
    {
        return value is >= MatchThreeTiles.FallTypeStart and < MatchThreeTiles.FallTypeEnd;
    }

    /// <summary>
    /// Everything above a hole (but row 0) falls a row.
    /// </summary>
    private void AddColumnFall(int holeY, int x)
    {
        _hasFall = true;

        for (var y = holeY - 1; y > 0; y--)
        {
            var tile = TileAt(x, y);

            if (tile is null || tile.Category is MatchThreeTiles.Empty or MatchThreeTiles.DropZone)
                continue;

            tile.AddFall();
            _hasFall = true;
        }
    }

    private void GenerateGrid()
    {
        _current = new MatchThreeTile?[Height + 1, Width];
    }

    /// <summary>
    /// Steps every tile once and puts the live ones on the board; then the falls, until the board settles. A board
    /// without a move is rebuilt.
    /// </summary>
    private void UpdateCurrentGridArray()
    {
        if (_updateDepth >= MaximumUpdateDepth)
        {
            _game.LogWarning($"level {_level.LevelId}'s board didn't settle");
            return;
        }

        _updateDepth++;

        try
        {
            GenerateGrid();

            // A tile's update can collect it, which can start a wave and change the list.
            for (var i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i];
                tile.Update();

                if (tile.State != MatchThreeTileState.Destroy)
                    SetTileAt(tile.X, tile.Y, tile);
            }

            if (_hasExploded)
            {
                _hasExploded = false;

                if (CheckFalls())
                {
                    UpdateCurrentGridArray();
                    return;
                }
            }

            if (!HasMove())
                LevelReshuffle();

            if (CheckFalls())
                UpdateCurrentGridArray();
        }
        finally
        {
            _updateDepth--;
        }
    }

    #endregion

    #region Queues

    private void InitRandomTileArray(int? category, MatchThreeTileState state)
    {
        _randomTiles = [];
        RandomTileAddTiles(category, state);
    }

    private void RandomTileAddTiles(int? category, MatchThreeTileState state)
    {
        foreach (var tile in _tiles)
        {
            if (tile.Category >= MatchThreeTiles.Visible && (category is null || tile.Category == category) && tile.State == state)
                _randomTiles.Add(tile);
        }
    }

    private MatchThreeTile? GetRandomTile()
    {
        if (_randomTiles.Count <= 0)
            return null;

        var index = GetRandomNumberFromQueue(0, _randomTiles.Count - 1);
        var tile = _randomTiles[index];
        _randomTiles.RemoveAt(index);

        return tile;
    }

    /// <summary>
    /// The next shared random number, in a range. The movie draws the same one.
    /// </summary>
    private int GetRandomNumberFromQueue(int start, int end)
    {
        if (_randomNumbers.Count == 0)
        {
            GenerateRandomNumbersForQueue(RandomQueueBatchSize);
            SendRandomNumberQueue();
        }

        var number = _randomNumbers.Dequeue();
        var result = start + number % (end - start + 1);

        while (_randomNumbers.Count < RandomQueueMinLength)
        {
            GenerateRandomNumbersForQueue(RandomQueueBatchSize);
            SendRandomNumberQueue();
        }

        return result;
    }

    private void GenerateRandomNumbersForQueue(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var number = (int)Math.Floor(_random.NextDouble() * RandomMaxValue);
            _randomNumbers.Enqueue(number);
            _unsentRandomNumbers.Add(number);
        }
    }

    private void SendRandomNumberQueue()
    {
        var list = _unsentRandomNumbers;
        _unsentRandomNumbers = [];
        _game.SendRandomNumberQueue(list);
    }

    /// <summary>
    /// The next tile off the queue drops in at the top of a column. Below the queue's minimum, more are queued.
    /// </summary>
    private void RegenTile(int x, int y)
    {
        if (_tileQueue.Count == 0)
        {
            // Not in step with the movie any more (it had no tile either); keep the board whole.
            _game.LogWarning($"level {_level.LevelId} ran out of queued tiles");
            GenerateTilesForQueue(TileQueueBatchSize, MatchThreeTiles.Any, MatchThreeTiles.Any);
            SendTileQueue(false);
        }

        var tile = _tileQueue.Dequeue();
        tile.SetPosition(x, y);
        tile.SetGrid(this);
        _tiles.Add(tile);

        if (_tileQueue.Count < TileQueueMinLength)
        {
            GenerateTilesForQueue(TileQueueBatchSize, MatchThreeTiles.Any, MatchThreeTiles.Any);
            SendTileQueue(false);
        }
    }

    /// <summary>
    /// Makes tiles for the queue; they're pending until sent (and, for a replaced queue, until the movie takes it).
    /// </summary>
    private void GenerateTilesForQueue(int count, int category, int value)
    {
        if (_tilePendingQueue.Count > 0)
            return;

        for (var i = 0; i < count; i++)
        {
            var tile = _generator.GenerateTile(category, value);
            _tilePendingQueue.Add(tile);
            _tileUnsentValues.Add(tile.NetworkValue);
        }
    }

    /// <summary>
    /// Sends the tiles not yet sent. <paramref name="reset"/> asks the movie to replace its queue if it holds as many
    /// tiles as the server's (it answers CLIENT_TILEQUEUE_ACCEPT or _REJECT); otherwise they're added to the end.
    /// </summary>
    private void SendTileQueue(bool reset)
    {
        if (_tileUnsentValues.Count <= 0)
            return;

        var list = _tileUnsentValues.ToArray();
        var resetCount = -1;

        if (reset)
            resetCount = _tileQueue.Count;
        else
            TileQueueAddPending();

        _game.SendTileQueue(resetCount, list);
    }

    private void TileQueueAddPending()
    {
        foreach (var tile in _tilePendingQueue)
            _tileQueue.Enqueue(tile);

        _tilePendingQueue = [];
        _tileUnsentValues = [];
    }

    #endregion

    #region Collecting

    internal void OnTileCollect(MatchThreeTile tile)
    {
        _level.HandleCollected(tile.Category, tile.Value);
    }

    #endregion

    #region Powerups (OnActionMsg)

    /// <summary>
    /// Dynamite: the pressed tile and then, up to its power, random idle neighbours (selectables, wildcards and
    /// blockers) of the tiles already blasted, drawn off the shared random queue. The movie's own server rejects a
    /// blast that draws a tile twice, which its client allows (it skips the tile and draws again), and stops drawing
    /// once it has as many tiles as the client sent, where the client may still draw; this replays the client's
    /// blast exactly, so both use up the same random numbers.
    /// </summary>
    public bool StartShatter(IReadOnlyList<string> list, int power)
    {
        _shatter1 = [];
        _shatter2 = [];

        var count = list.Count / 2;

        if (count > power)
            return false;

        var index = 0;
        var tile = count > 0 ? TileAt(ParseInt(list[0]), ParseInt(list[1])) : null;

        while (power > 0 && tile is not null)
        {
            if (tile.State == MatchThreeTileState.Idle)
            {
                if (index >= count || tile != TileAt(ParseInt(list[index * 2]), ParseInt(list[index * 2 + 1])))
                    return false;

                index++;

                var x = tile.X;
                var y = tile.Y;

                tile.SetState(MatchThreeTileState.Explode);
                _hasExploded = true;
                RemoveTileFromGrid(tile);
                SetTileAt(x, y, null);
                ShatterAddNeighbors(x, y);
                power--;
            }

            if (power > 0)
            {
                if (_shatter1.Count <= 0)
                {
                    _shatter1 = [.. _shatter2];
                    _shatter2 = [];
                }

                tile = ShatterGetNextTile();
            }
        }

        if (index != count)
            return false;

        _shatter1 = [];
        _shatter2 = [];
        UpdateCurrentGridArray();

        return true;
    }

    private MatchThreeTile? ShatterGetNextTile()
    {
        if (_shatter1.Count == 0)
            return null;

        var index = GetRandomNumberFromQueue(0, _shatter1.Count - 1);
        var tile = _shatter1[index];
        _shatter1.RemoveAt(index);

        return tile;
    }

    private void ShatterAddNeighbors(int x, int y)
    {
        ShatterAddTileToArray2(TileAt(x, y + 1));
        ShatterAddTileToArray2(TileAt(x, y - 1));
        ShatterAddTileToArray2(TileAt(x + 1, y));
        ShatterAddTileToArray2(TileAt(x - 1, y));
    }

    private void ShatterAddTileToArray2(MatchThreeTile? tile)
    {
        if (tile is null || tile.State != MatchThreeTileState.Idle)
            return;

        if (tile.Category is MatchThreeTiles.Selectable or MatchThreeTiles.Wildcard or MatchThreeTiles.Blocker && !_shatter2.Contains(tile))
            _shatter2.Add(tile);
    }

    /// <summary>
    /// Wild tiles: the pressed tile and random idle selectables turn into wildcards, as many as its power.
    /// </summary>
    public bool StartWildTiles(IReadOnlyList<string> list, bool useTile, int power)
    {
        if (list.Count / 2 > power)
            return false;

        MatchThreeTile? tile = null;

        if (useTile)
            tile = TileAt(ParseInt(list[0]), ParseInt(list[1]));

        InitRandomTileArray(MatchThreeTiles.Selectable, MatchThreeTileState.Idle);

        for (var i = 0; i < power; i++)
        {
            if (i != 0 || tile is null)
                tile = GetRandomTile();

            if (tile is null)
                break;

            if (i * 2 + 1 >= list.Count || TileAt(ParseInt(list[i * 2]), ParseInt(list[i * 2 + 1])) != tile)
                return false;

            DoWildTiles(tile);
        }

        UpdateCurrentGridArray();
        return true;
    }

    /// <summary>
    /// Replaces a tile with a wildcard (a converted tile, or one wild tiles picked).
    /// </summary>
    internal void DoWildTiles(MatchThreeTile tile)
    {
        var x = tile.X;
        var y = tile.Y;

        RemoveTileFromGrid(tile);
        tile.SetState(MatchThreeTileState.Destroy);

        var wildcard = MatchThreeTileGenerator.AddSpecificTile(MatchThreeTiles.Wildcard, MatchThreeTiles.Wildcard);
        SetTileAt(x, y, wildcard);
        wildcard.SetState(MatchThreeTileState.Appear);
        wildcard.SetPosition(x, y);
        wildcard.SetGrid(this);
        _tiles.Add(wildcard);
    }

    /// <summary>
    /// Destruct: the pressed tile and random idle selectables and blockers blow up, as many as its power. They
    /// aren't collected.
    /// </summary>
    public bool StartDestruction(IReadOnlyList<string> list, bool useTile, int power)
    {
        if (list.Count / 2 > power)
            return false;

        MatchThreeTile? tile = null;

        if (useTile)
            tile = TileAt(ParseInt(list[0]), ParseInt(list[1]));

        InitRandomTileArray(MatchThreeTiles.Selectable, MatchThreeTileState.Idle);
        RandomTileAddTiles(MatchThreeTiles.Blocker, MatchThreeTileState.Idle);

        for (var i = 0; i < power; i++)
        {
            if (i != 0 || tile is null)
                tile = GetRandomTile();

            if (tile is null)
                break;

            if (i * 2 + 1 >= list.Count || TileAt(ParseInt(list[i * 2]), ParseInt(list[i * 2 + 1])) != tile)
                return false;

            RemoveTileFromGrid(tile);
            tile.SetState(MatchThreeTileState.Explode);
            _hasExploded = true;
        }

        UpdateCurrentGridArray();
        return true;
    }

    /// <summary>
    /// Shuffle: the pressed tile turns wild, and random idle tiles (one fewer than its power) swap places in a ring,
    /// each skipping on past a place that already holds its own kind.
    /// </summary>
    public bool StartShuffle(IReadOnlyList<string> clientList, bool useTile, int power)
    {
        var list = clientList.ToList();
        var count = power;

        if (useTile)
        {
            var pressed = TileAt(ParseInt(list.ElementAtOrDefault(0)), ParseInt(list.ElementAtOrDefault(1)));

            if (pressed is null)
                return false;

            DoWildTiles(pressed);
            count--;

            list.RemoveRange(0, Math.Min(2, list.Count));
        }

        InitRandomTileArray(null, MatchThreeTileState.Idle);

        if (_randomTiles.Count < count)
            count = _randomTiles.Count;

        if (list.Count / 2 > count)
            return false;

        var positions = new (int X, int Y)[Math.Max(count, 0)];
        var moved = new MatchThreeTile?[Math.Max(count, 0)];

        for (var i = 0; i < count; i++)
        {
            var tile = GetRandomTile();

            if (tile is null)
                return false;

            if (i * 2 + 1 >= list.Count || TileAt(ParseInt(list[i * 2]), ParseInt(list[i * 2 + 1])) != tile)
                return false;

            positions[i] = (tile.X, tile.Y);
            moved[(i + 1) % count] = tile;
            tile.SetState(MatchThreeTileState.Disappear);
        }

        for (var i = 0; i < count; i++)
        {
            var tile = moved[i]!;
            var position = positions[i];
            var there = TileAt(position.X, position.Y);
            var skip = 0;

            while (there is not null && there.Value == tile.Value && i + skip < count)
            {
                skip++;

                if (i + skip < count)
                {
                    position = positions[i + skip];
                    there = TileAt(position.X, position.Y);
                }
                else
                {
                    there = null;
                }
            }

            if (i + skip >= count)
                position = positions[i];
            else if (skip != 0)
                positions[i + skip] = positions[i];

            tile.SetPosition(position.X, position.Y);
            tile.SetState(MatchThreeTileState.Appear);
        }

        _hasExploded = true;
        UpdateCurrentGridArray();

        return true;
    }

    /// <summary>
    /// A loot drop (unused by the shipped data): the movie's server answers it with a new board.
    /// </summary>
    public bool StartLootDrop()
    {
        LevelReshuffle();
        return true;
    }

    private void RemoveTileFromGrid(MatchThreeTile tile)
    {
        _tiles.Remove(tile);
    }

    #endregion

    /// <summary>
    /// ActionScript's parseInt: the leading integer, or <see cref="int.MinValue"/> (no cell) when there's none.
    /// </summary>
    private static int ParseInt(string? text)
    {
        return TryParseInt(text, out var value) ? value : int.MinValue;
    }

    internal static bool TryParseInt(string? text, out int value)
    {
        value = 0;

        if (text is null)
            return false;

        var span = text.AsSpan().TrimStart();
        var length = 0;

        if (length < span.Length && span[length] is '-' or '+')
            length++;

        var digitsStart = length;

        while (length < span.Length && char.IsAsciiDigit(span[length]))
            length++;

        if (length == digitsStart)
            return false;

        return int.TryParse(span[..length], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }
}
