using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Sanctuary.Game.VehicleGames.Soccer;

/// <summary>
/// A soccer match while the ball is in play, with no zone or packets: the ball's flight, who has it, the AI players
/// (chasing the ball, keeping their places, dribbling at goal, shooting, passing and tackling; the goalies guarding
/// their mouths), the player's tackles and kicks, the power-ups and the goals. What happens is told through the
/// callbacks, which <see cref="SoccerMatch"/> turns into packets.
/// </summary>
public sealed class SoccerSimulation
{
    private const int DecisionMs = 200;
    private const float ShootingRange = 17f;
    private const float TackleReach = 1.6f;
    private const float PlayerTackleReach = 2.4f;
    private const float PickupReach = 1.1f;

    private readonly Random _random;
    private readonly List<SoccerPlayer> _players = [];
    private readonly Dictionary<ulong, SoccerPlayer> _playersByGuid = [];
    private readonly (int Kind, long Until)[] _pickups;

    private long _nextDecisionAt;
    private long _nextPickupAt;

    public SoccerSimulation(Vector4 bounds, Vector3 centreSpot, IReadOnlyList<Vector2> pickupSlots, Random random)
    {
        Bounds = bounds;
        CentreSpot = centreSpot;
        PickupSlots = pickupSlots;
        _random = random;

        _pickups = new (int, long)[pickupSlots.Count];
    }

    /// <summary>
    /// Min x, min z, max x, max z.
    /// </summary>
    public Vector4 Bounds { get; }

    public Vector3 CentreSpot { get; }

    public IReadOnlyList<Vector2> PickupSlots { get; }

    public SoccerBall Ball { get; } = new();

    public IReadOnlyList<SoccerPlayer> Players => _players;

    public int[] Score { get; } = new int[2];

    /// <summary>
    /// This half's team tallies, as the scoreboard shows them.
    /// </summary>
    public int[] Shots { get; } = new int[2];
    public int[] Knockdowns { get; } = new int[2];
    public int[] Pickups { get; } = new int[2];
    public int[] StealCount { get; } = new int[2];

    /// <summary>
    /// The ball changed hands: (the new holder, the one who had it or 0).
    /// </summary>
    public Action<SoccerPlayer, ulong>? Acquired { get; set; }

    /// <summary>
    /// A holder let the ball go (a kick, a throw).
    /// </summary>
    public Action<SoccerPlayer>? Freed { get; set; }

    /// <summary>
    /// A lob: where it comes down and in how long.
    /// </summary>
    public Action<Vector3, float>? Lobbed { get; set; }

    /// <summary>
    /// The ball bounced (1 off a goalie, 0 anything else).
    /// </summary>
    public Action<int>? Bounced { get; set; }

    /// <summary>
    /// A goal: (the goal, 0 low x or 1 high x; the scoring team; who scored, if anyone did).
    /// </summary>
    public Action<int, int, SoccerPlayer?>? Scored { get; set; }

    public Action<int, int, float>? PickupSpawned { get; set; }
    public Action<int>? PickupRemoved { get; set; }
    public Action<SoccerPlayer, int, int>? PickupTaken { get; set; }
    public Action<SoccerPlayer>? PickupEnded { get; set; }

    public bool TryGetPlayer(ulong guid, out SoccerPlayer player) => _playersByGuid.TryGetValue(guid, out player!);

    public void Add(SoccerPlayer player)
    {
        _players.Add(player);
        _playersByGuid[player.Guid] = player;
    }

    public void Remove(SoccerPlayer player)
    {
        _players.Remove(player);
        _playersByGuid.Remove(player.Guid);

        if (Ball.Holder == player.Guid)
            Release(player, Vector3.Zero);
    }

    private float CentreZ => CentreSpot.Z;

    /// <summary>
    /// Everybody back on their spots and the ball on the centre spot (a kick-off).
    /// </summary>
    public void ResetFormation()
    {
        foreach (var player in _players)
        {
            player.Location = player.KickOff;
            player.Facing = new Vector3(player.Team == 0 ? -1f : 1f, 0f, 0f);
            player.SetStick(0f, 0f);
            player.SetState(1);
            player.BusyUntil = 0;
        }

        Ball.Holder = 0;
        Ball.Position = CentreSpot;
        Ball.Velocity = Vector3.Zero;
        Ball.Lobbed = false;
        Ball.KickTimer = 0f;
        Ball.InPlay = true;
    }

    /// <summary>
    /// The half's tallies start again.
    /// </summary>
    public void NewPeriod()
    {
        Array.Clear(Shots);
        Array.Clear(Knockdowns);
        Array.Clear(Pickups);
        Array.Clear(StealCount);
    }

    public void Update(long now, float deltaSeconds)
    {
        if (now >= _nextDecisionAt)
        {
            _nextDecisionAt = now + DecisionMs;

            foreach (var player in _players)
            {
                if (!player.IsPlayer)
                    Decide(player, now);
            }
        }

        foreach (var player in _players)
        {
            if (!player.IsPlayer)
                Run(player, now, deltaSeconds);
        }

        UpdateBall(now, deltaSeconds);
        UpdatePickups(now);
    }

    #region The ball

    private void UpdateBall(long now, float deltaSeconds)
    {
        if (!Ball.IsLoose)
        {
            if (TryGetPlayer(Ball.Holder, out var holder))
            {
                var held = holder.Location + new Vector3(0f, SoccerRules.BallRadius, 0f) + holder.Facing * 0.6f;
                held.Z = Math.Clamp(held.Z, Bounds.Y, Bounds.W);

                Ball.Position = held;
            }

            return;
        }

        var (position, velocity, bounced) = SoccerRules.Step(Ball.Position, Ball.Velocity, deltaSeconds, Bounds, CentreZ);

        Ball.Position = position;
        Ball.Velocity = velocity;

        if (bounced)
            Bounced?.Invoke(0);

        if (!Ball.InPlay)
            return;

        if (SoccerRules.GoalAt(position, Bounds, CentreZ) is { } goal)
        {
            Ball.InPlay = false;

            var scoringTeam = 1 - SoccerRules.Defender(goal);

            TryGetPlayer(Ball.LastTouch, out var scorer);

            // An own goal scores for the other team, but nobody on it.
            if (scorer is not null && scorer.Team != scoringTeam)
                scorer = null;

            Score[scoringTeam]++;
            Shots[scoringTeam]++;

            if (scorer is not null)
            {
                scorer.Goals++;
                scorer.Score += SoccerRules.GoalPoints;
                scorer.StatsChanged = true;
            }

            Scored?.Invoke(goal, scoringTeam, scorer);
            return;
        }

        // Whoever reaches a loose ball low enough has it; goalies dive further.
        var nearest = _players
            .Where(player => now >= player.BusyUntil || player.IsGoalie)
            .Select(player => (Player: player, Distance: Vector2.Distance(new Vector2(player.Location.X, player.Location.Z), new Vector2(position.X, position.Z))))
            .Where(candidate => candidate.Distance < (candidate.Player.IsGoalie ? 2f : SoccerRules.PickupReach) && position.Y < (candidate.Player.IsGoalie ? 2.4f : SoccerRules.PickupHeight))
            .OrderBy(candidate => candidate.Distance)
            .Select(candidate => candidate.Player)
            .FirstOrDefault();

        // The last kicker can't take it straight back.
        if (nearest is not null && !(nearest.Guid == Ball.LastTouch && Ball.KickTimer > 8f))
        {
            if (nearest.IsGoalie && velocity.Length() > 6f)
                Bounced?.Invoke(1);

            Take(nearest, 0, now);
        }
    }

    private void Take(SoccerPlayer player, ulong previous, long now)
    {
        Ball.Holder = player.Guid;
        Ball.Velocity = Vector3.Zero;
        Ball.Lobbed = false;
        Ball.LastTouch = player.Guid;

        player.HeldSince = now;

        if (!player.IsPlayer)
            player.SetState(player.IsGoalie ? (byte)59 : (byte)10);

        Acquired?.Invoke(player, previous);
    }

    /// <summary>
    /// The holder lets the ball go with a velocity (a kick, a throw, a pass).
    /// </summary>
    public void Release(SoccerPlayer player, Vector3 velocity, bool lob = false)
    {
        if (Ball.Holder != player.Guid)
            return;

        Ball.Holder = 0;
        Ball.Position = player.Location + new Vector3(0f, SoccerRules.KickHeight, 0f) + player.Facing * 0.5f;
        Ball.Velocity = velocity;
        Ball.Lobbed = lob;
        Ball.KickTimer = velocity.LengthSquared() > 1f ? 9.75f : 0f;
        Ball.LastTouch = player.Guid;

        Freed?.Invoke(player);

        if (lob)
        {
            // Where a lob comes down: up and back to the ground under gravity, at its speed along the ground.
            var flight = (velocity.Y + MathF.Sqrt(velocity.Y * velocity.Y + 2f * SoccerRules.Gravity * (Ball.Position.Y - SoccerRules.BallRadius))) / SoccerRules.Gravity;
            var landing = Ball.Position + new Vector3(velocity.X, 0f, velocity.Z) * flight * (1f - SoccerRules.AirDrag * flight / 2f);

            Lobbed?.Invoke(landing with { Y = 0.35f }, flight);
        }
    }

    /// <summary>
    /// A shot at the other team's goal, somewhere in its mouth.
    /// </summary>
    public void Shoot(SoccerPlayer player, float speed, bool lob = false)
    {
        var target = new Vector3(SoccerRules.TargetGoalX(player.Team, Bounds), 0f, CentreZ + ((float)_random.NextDouble() * 2f - 1f) * (SoccerRules.GoalHalfWidth - 0.8f));

        Shots[player.Team]++;
        player.Facing = Flat(target - player.Location);

        Release(player, SoccerRules.Kick(player.Location, target, speed, lob ? 9f : 1f + (float)_random.NextDouble() * 2f), lob);
    }

    #endregion

    #region The AI

    /// <summary>
    /// What an AI player does next.
    /// </summary>
    private void Decide(SoccerPlayer player, long now)
    {
        if (now < player.BusyUntil)
            return;

        if (Ball.Holder == player.Guid)
        {
            Attack(player, now);
            return;
        }

        if (player.IsGoalie)
        {
            Guard(player);
            return;
        }

        var ballFlat = Flat(Ball.Position);

        if (Ball.IsLoose)
        {
            // The nearest of each team goes for it; the others keep their places, drawn towards it.
            if (Nearest(player.Team, ballFlat) == player)
                RunTo(player, ballFlat, (byte)2);
            else
                RunTo(player, Support(player, ballFlat), (byte)2);

            return;
        }

        if (!TryGetPlayer(Ball.Holder, out var holder))
            return;

        if (holder.Team == player.Team)
        {
            // Up the pitch with them, spread out.
            RunTo(player, Support(player, Flat(holder.Location)) + new Vector3(SoccerRules.TargetGoalX(player.Team, Bounds) < CentreSpot.X ? -6f : 6f, 0f, 0f), (byte)2);
            return;
        }

        // The nearest goes in for the tackle.
        if (Nearest(player.Team, Flat(holder.Location)) == player)
        {
            RunTo(player, Flat(holder.Location), (byte)2);

            if (Distance(player, holder) < TackleReach && now >= player.NextTackleAt)
            {
                player.NextTackleAt = now + 1200;

                if (_random.NextDouble() < (holder.IsPlayer ? 0.35 : 0.45))
                    Steal(player, holder, now, slide: false);
            }

            return;
        }

        RunTo(player, Support(player, Flat(holder.Location)), (byte)2);
    }

    /// <summary>
    /// With the ball: dribble at goal, shoot in range, now and then pass forward, and get rid of it when pressed.
    /// </summary>
    private void Attack(SoccerPlayer player, long now)
    {
        var goal = new Vector3(SoccerRules.TargetGoalX(player.Team, Bounds), 0f, CentreZ);
        var toGoal = MathF.Abs(goal.X - player.Location.X);

        if (player.IsGoalie)
        {
            // Hold it a moment, then throw it out to a team mate.
            if (now - player.HeldSince < 1500)
                return;

            var mate = _players.Where(other => other.Team == player.Team && !other.IsGoalie).MinBy(other => Distance(player, other));

            player.SetState(62);
            player.BusyUntil = now + 800;

            Release(player, SoccerRules.Kick(player.Location, mate?.Location ?? CentreSpot, 13f, 4f));
            return;
        }

        var pressed = _players.Any(other => other.Team != player.Team && Distance(player, other) < 2.2f);

        if (toGoal < ShootingRange || (pressed && toGoal < ShootingRange * 1.5f))
        {
            var super = _random.NextDouble() < 0.2;

            player.SetState(super ? (byte)22 : (byte)17);
            player.SetStick(0f, 0f);
            player.BusyUntil = now + 700;

            Shoot(player, super ? 22f : 16f + (float)_random.NextDouble() * 4f, lob: !super && _random.NextDouble() < 0.15);
            return;
        }

        var ahead = _players.Where(other => other.Team == player.Team && !other.IsGoalie && other != player
                && MathF.Abs(goal.X - other.Location.X) < toGoal - 4f
                && !_players.Any(opponent => opponent.Team != player.Team && Distance(other, opponent) < 3f))
            .MinBy(other => Distance(player, other));

        if (ahead is not null && (pressed || _random.NextDouble() < 0.08))
        {
            player.SetState(27);
            player.SetStick(0f, 0f);
            player.BusyUntil = now + 600;
            player.Facing = Flat(ahead.Location - player.Location);

            Release(player, SoccerRules.Kick(player.Location, ahead.Location, 13f, 2f));
            return;
        }

        RunTo(player, goal, (byte)11);
    }

    /// <summary>
    /// A goalie keeps between the ball and the middle of their goal, on the line.
    /// </summary>
    private void Guard(SoccerPlayer goalie)
    {
        var lineX = SoccerRules.TargetGoalX(1 - goalie.Team, Bounds);
        var x = lineX + (lineX < CentreSpot.X ? 1.5f : -1.5f);
        var z = Math.Clamp(Ball.Position.Z, CentreZ - SoccerRules.GoalHalfWidth + 0.5f, CentreZ + SoccerRules.GoalHalfWidth - 0.5f);

        RunTo(goalie, new Vector3(x, 0f, z), (byte)2);
    }

    private Vector3 Support(SoccerPlayer player, Vector3 ball)
    {
        // Their kick-off spot, pulled a third of the way towards the ball.
        var spot = Flat(player.KickOff);

        return spot + (ball - spot) * 0.35f;
    }

    private SoccerPlayer? Nearest(int team, Vector3 point) =>
        _players.Where(player => player.Team == team && !player.IsGoalie).MinBy(player => Vector3.Distance(Flat(player.Location), point));

    private static void RunTo(SoccerPlayer player, Vector3 target, byte runState)
    {
        var (x, y, _) = SoccerRules.Stick(player.Location, target);

        player.SetStick(x, y);
        player.SetState(x == 0f && y == 0f ? (byte)1 : runState);
    }

    /// <summary>
    /// An AI player runs by its stick, as the client moves it.
    /// </summary>
    private void Run(SoccerPlayer player, long now, float deltaSeconds)
    {
        if (now < player.BusyUntil)
            return;

        var direction = SoccerRules.StickDirection(player.StickX, player.StickY);

        if (direction == Vector3.Zero)
            return;

        var location = player.Location + direction * player.RunSpeed * deltaSeconds;

        location.X = Math.Clamp(location.X, Bounds.X - 1f, Bounds.Z + 1f);
        location.Z = Math.Clamp(location.Z, Bounds.Y, Bounds.W);

        player.Location = location;
        player.Facing = direction;
    }

    #endregion

    #region Tackles

    /// <summary>
    /// A steal (or, sliding, a knockdown): the tackler has the ball.
    /// </summary>
    public void Steal(SoccerPlayer tackler, SoccerPlayer holder, long now, bool slide)
    {
        if (Ball.Holder != holder.Guid)
            return;

        if (!tackler.IsPlayer)
        {
            tackler.SetState(slide ? (byte)36 : (byte)35);
            tackler.BusyUntil = now + 500;
        }

        if (!holder.IsPlayer)
        {
            holder.SetState(slide ? (byte)41 : (byte)39);
            holder.SetStick(0f, 0f);
            holder.BusyUntil = now + (slide ? 1800 : 900);
        }

        tackler.Steals++;
        tackler.Score += SoccerRules.StealPoints;
        tackler.StatsChanged = true;
        StealCount[tackler.Team]++;

        if (slide)
        {
            tackler.Knockdowns++;
            tackler.Score += SoccerRules.KnockdownPoints;
            Knockdowns[tackler.Team]++;
        }

        Take(tackler, holder.Guid, now);
    }

    /// <summary>
    /// The player's client says what they're doing: a tackle near the holder takes the ball, a kick with it lets it
    /// go along their facing (harder with more charge; a lob kick goes up).
    /// </summary>
    public void OnPlayerState(SoccerPlayer player, byte state, byte charge, Vector3 facing, long now)
    {
        player.State = state;
        player.Charge = charge;

        if (facing.LengthSquared() > 1e-4f)
            player.Facing = Vector3.Normalize(Flat(facing));

        if (state is 35 or 36 && !Ball.IsLoose && Ball.Holder != player.Guid && TryGetPlayer(Ball.Holder, out var holder)
            && holder.Team != player.Team && Distance(player, holder) < PlayerTackleReach && now >= player.NextTackleAt)
        {
            player.NextTackleAt = now + 800;

            if (_random.NextDouble() < (state == 36 ? 0.8 : 0.65))
                Steal(player, holder, now, slide: state == 36);

            return;
        }

        if (state is >= 16 and <= 34 && Ball.Holder == player.Guid)
        {
            var lob = state is 19 or 20 or 21 or 26 or 28 or 30;
            var speed = state is 22 or 23 ? 22f : 13f + charge / 100f * 8f;
            var target = player.Location + player.Facing * 20f;

            if (MathF.Abs(SoccerRules.TargetGoalX(player.Team, Bounds) - player.Location.X) < 25f)
                Shots[player.Team]++;

            Release(player, SoccerRules.Kick(player.Location, target, speed, lob ? 9f : 1.5f), lob);
        }

        // A dodge past a defender (live's "Pickups" tally went up with these).
        if (state is 4 or 5 or 6 && Ball.Holder == player.Guid && _players.Any(other => other.Team != player.Team && Distance(player, other) < 3f))
        {
            player.Dodges++;
            player.Score += SoccerRules.DodgePoints;
            player.StatsChanged = true;
            Pickups[player.Team]++;
        }
    }

    #endregion

    #region Power-ups

    private void UpdatePickups(long now)
    {
        for (var slot = 0; slot < _pickups.Length; slot++)
        {
            var (kind, until) = _pickups[slot];

            if (kind != 0 && now >= until)
            {
                _pickups[slot] = (0, 0);
                PickupRemoved?.Invoke(slot);
                continue;
            }

            if (kind == 0)
                continue;

            var at = PickupSlots[slot];

            var taker = _players.FirstOrDefault(player => player.PickupKind == 0
                && Vector2.Distance(new Vector2(player.Location.X, player.Location.Z), at) < PickupReach);

            if (taker is null)
                continue;

            _pickups[slot] = (0, 0);

            taker.PickupKind = kind;
            taker.PickupUntil = now + 20_000;

            PickupTaken?.Invoke(taker, slot, kind);
        }

        foreach (var player in _players)
        {
            if (player.PickupKind == 0 || now < player.PickupUntil)
                continue;

            player.PickupKind = 0;
            PickupEnded?.Invoke(player);
        }

        if (now < _nextPickupAt)
            return;

        _nextPickupAt = now + 8000 + _random.Next(5000);

        var empty = Enumerable.Range(0, _pickups.Length).Where(slot => _pickups[slot].Kind == 0).ToList();

        if (empty.Count == 0)
            return;

        var chosen = empty[_random.Next(empty.Count)];

        // Speed Shoes, Toughness Charm, Quick Charge (the kinds live spawned).
        var newKind = new[] { 1, 2, 4 }[_random.Next(3)];
        var lifetime = 22f + (float)_random.NextDouble() * 9f;

        _pickups[chosen] = (newKind, now + (long)(lifetime * 1000f));

        PickupSpawned?.Invoke(chosen, newKind, lifetime);
    }

    /// <summary>
    /// Every power-up off the pitch (a goal clears them).
    /// </summary>
    public void ClearPickups()
    {
        for (var slot = 0; slot < _pickups.Length; slot++)
        {
            if (_pickups[slot].Kind == 0)
                continue;

            _pickups[slot] = (0, 0);
            PickupRemoved?.Invoke(slot);
        }
    }

    #endregion

    private static float Distance(SoccerPlayer a, SoccerPlayer b) =>
        Vector2.Distance(new Vector2(a.Location.X, a.Location.Z), new Vector2(b.Location.X, b.Location.Z));

    private static Vector3 Flat(Vector3 vector) => new(vector.X, 0f, vector.Z);
}
