namespace Sanctuary.Game.Minigames.MatchThree;

/// <summary>
/// Where a tile is in its life on the server's board. The movie animates each of these; the server steps a tile
/// through them instantly (ServerState.onUpdate calls the method named after the state), except that a tile that
/// fell or appeared waits in <see cref="Pending"/> until the movie reports it idle (OnIdleMsg).
/// </summary>
public enum MatchThreeTileState
{
    Create,
    Idle,

    /// <summary>
    /// Settled on the server, still moving in the movie (its "pending").
    /// </summary>
    Pending,
    Appear,
    Disappear,
    Explode,
    Destroy,
    Fall
}

/// <summary>
/// A tile on the server's board (the movies' core.ServerTile): its category and value, where it is, and what it's
/// waiting to do (fall some rows, be collected, fall off the bottom into a drop zone, turn wild).
/// </summary>
public sealed class MatchThreeTile
{
    private MatchThreeGrid? _grid;

    private bool _collect;
    private bool _convert;
    private int _rowsToFall;

    public MatchThreeTile(int category, int value, bool locked = false)
    {
        Category = category;
        Value = value;
        IsLocked = locked;
        State = MatchThreeTileState.Create;
    }

    public int Category { get; }

    /// <summary>
    /// A wildcard in a chain takes the value of the chain's selectables, so it counts for their quota.
    /// </summary>
    public int Value { get; private set; }

    public int X { get; private set; }
    public int Y { get; private set; }

    public MatchThreeTileState State { get; private set; }

    /// <summary>
    /// A locked selectable takes a chain to unlock before one can collect it.
    /// </summary>
    public bool IsLocked { get; private set; }

    public bool IsCollect => _collect;
    public bool IsConvert => _convert;

    /// <summary>
    /// Flagged to drop into the drop zone below it; the movie reports when it has (CLIENT_FALL).
    /// </summary>
    public bool IsFallOffBottom { get; private set; }

    /// <summary>
    /// The value the movie knows the tile by: a locked selectable is sent as its locked value (500s).
    /// </summary>
    public int NetworkValue => IsLocked && Category == MatchThreeTiles.Selectable ? Value + MatchThreeTiles.LockDelta : Value;

    internal void SetGrid(MatchThreeGrid grid) => _grid = grid;

    internal void SetPosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    internal void SetState(MatchThreeTileState state) => State = state;

    internal void SetChainValue(int value) => Value = value;

    internal void Unlock() => IsLocked = false;

    internal void Lock() => IsLocked = true;

    internal void AddCollect() => _collect = true;

    internal void AddConvert() => _convert = true;

    internal void AddFallOffBottom() => IsFallOffBottom = true;

    internal void AddFall()
    {
        _rowsToFall++;
        State = MatchThreeTileState.Fall;
    }

    internal void Convert()
    {
        if (!_convert)
            return;

        _convert = false;
        _grid?.DoWildTiles(this);
    }

    /// <summary>
    /// Runs the tile's state once (ServerState.onUpdate): a new tile is idle, an appearing one waits for the movie,
    /// an exploding one is gone (and collected, if a chain or a drop zone took it), a falling one lands.
    /// </summary>
    internal void Update()
    {
        switch (State)
        {
            case MatchThreeTileState.Create:
                State = MatchThreeTileState.Idle;
                break;

            case MatchThreeTileState.Appear:
                State = MatchThreeTileState.Pending;
                break;

            case MatchThreeTileState.Disappear:
                State = MatchThreeTileState.Destroy;
                break;

            case MatchThreeTileState.Explode:
                State = MatchThreeTileState.Destroy;

                if (_collect)
                    _grid?.OnTileCollect(this);
                break;

            case MatchThreeTileState.Fall:
                OnFallFinished();
                break;
        }
    }

    /// <summary>
    /// The movie's version also asks the grid whether the tile landed just above a drop zone, but that check reads
    /// the tile under it before it's set and never fires; the grid's drop zone scan flags those tiles instead, as the
    /// movie's own grid does.
    /// </summary>
    private void OnFallFinished()
    {
        Y += _rowsToFall;
        _rowsToFall = 0;
        State = MatchThreeTileState.Pending;

        if (_collect)
        {
            _collect = false;
            _grid?.OnTileCollect(this);
        }
    }

    public override string ToString() => $"{Value}@{X},{Y} {State}";
}
