using System.Collections.Generic;
using System.Linq;

namespace Sanctuary.Game.VehicleGames;

/// <summary>
/// How a kart race or a derby keeps its players' clients in step, with no zone or packets. Everyone coming is waited
/// for to register (up to <see cref="ArenaMatch.JoinWaitMs"/> after the first), then every player given a vehicle
/// must acknowledge each game state, and echo each countdown number, before the game goes on. A player who leaves is
/// waited for no more; one who stops answering is waited for <see cref="ArenaMatch.AckTimeoutMs"/>.
/// </summary>
public sealed class ArenaHandshake
{
    private readonly Dictionary<ulong, ulong> _registered = [];
    private readonly HashSet<ulong> _seated = [];
    private readonly HashSet<ulong> _acks = [];
    private readonly HashSet<ulong> _echoes = [];

    private long _firstRegisteredAt;

    /// <summary>
    /// 0 until the setup, then the game state the clients were last sent.
    /// </summary>
    public int GameState { get; private set; }

    public long GameStateAt { get; private set; }

    /// <summary>
    /// The countdown number last sent, or -1.
    /// </summary>
    public int Countdown { get; private set; } = -1;

    public long CountdownAt { get; private set; }

    /// <summary>
    /// When the last of the players echoed <see cref="Countdown"/> (0 until they all have).
    /// </summary>
    public long CountdownEchoedAt { get; private set; }

    /// <summary>
    /// The players given a vehicle at the setup, still in the game.
    /// </summary>
    public IReadOnlyCollection<ulong> Seated => _seated;

    /// <summary>
    /// A player's client switched its game on and registered, with its clock. Only before the setup.
    /// </summary>
    public bool Register(ulong guid, ulong clock, long now)
    {
        if (GameState != 0 || !_registered.TryAdd(guid, clock))
            return false;

        if (_firstRegisteredAt == 0)
            _firstRegisteredAt = now;

        return true;
    }

    public bool IsRegistered(ulong guid) => _registered.ContainsKey(guid);

    /// <summary>
    /// The clock a player's client registered with (its own vehicle's states carry it).
    /// </summary>
    public ulong ClockOf(ulong guid) => _registered.GetValueOrDefault(guid);

    /// <summary>
    /// Time to set up: someone has registered, and so has everyone the game expects, or they've had long enough.
    /// </summary>
    public bool ReadyToSetUp(IEnumerable<ulong> expected, long now) =>
        _registered.Count > 0 && (expected.All(_registered.ContainsKey) || now - _firstRegisteredAt > ArenaMatch.JoinWaitMs);

    /// <summary>
    /// The player has a vehicle: the game waits on them from now on.
    /// </summary>
    public void Seat(ulong guid) => _seated.Add(guid);

    public bool IsSeated(ulong guid) => _seated.Contains(guid);

    public void SetGameState(int state, long now)
    {
        GameState = state;
        GameStateAt = now;

        _acks.Clear();
    }

    public void Acknowledge(ulong guid, int state)
    {
        if (state == GameState && _seated.Contains(guid))
            _acks.Add(guid);
    }

    /// <summary>
    /// Every seated player has acknowledged the game state (or it's been too long).
    /// </summary>
    public bool Acknowledged(long now, int timeoutMs = ArenaMatch.AckTimeoutMs) =>
        _seated.All(_acks.Contains) || now - GameStateAt > timeoutMs;

    public void StartCountdown(int count, long now)
    {
        Countdown = count;
        CountdownAt = now;
        CountdownEchoedAt = 0;

        _echoes.Clear();
    }

    public void Echo(ulong guid, int count, long now)
    {
        if (count == Countdown && _seated.Contains(guid) && _echoes.Add(guid) && _seated.All(_echoes.Contains))
            CountdownEchoedAt = now;
    }

    /// <summary>
    /// The next number is due: the first <paramref name="firstMs"/> after the 4 (live never saw an echo of it), the
    /// rest a moment after everyone echoed the last, or 1.5 s after it went.
    /// </summary>
    public bool CountdownDue(long now, int firstMs) => Countdown == 4
        ? now - CountdownAt >= firstMs
        : (CountdownEchoedAt != 0 && now - CountdownEchoedAt >= 300) || now - CountdownAt >= 1500;

    public void EndCountdown() => Countdown = -1;

    /// <summary>
    /// A player left: nothing waits on them any more.
    /// </summary>
    public void Leave(ulong guid, long now)
    {
        _registered.Remove(guid);
        _seated.Remove(guid);
        _acks.Remove(guid);
        _echoes.Remove(guid);

        if (Countdown >= 0 && CountdownEchoedAt == 0 && _seated.Count > 0 && _seated.All(_echoes.Contains))
            CountdownEchoedAt = now;
    }
}
