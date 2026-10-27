using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Sanctuary.Core.Extensions;
using Sanctuary.Core.IO;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions.Combat;
using Sanctuary.Game.Resources.Definitions.Rewards;
using Sanctuary.Packet;

namespace Sanctuary.Game.Entities;

/// <summary>
/// An orb or grenade's hold on a mob (see <see cref="NpcCombat.ApplyControl"/>).
/// </summary>
public enum MobControl
{
    None,

    /// <summary>
    /// Stands there doing nothing (stun, freeze, cocoon, confusion).
    /// </summary>
    Stun,

    /// <summary>
    /// A stun that any hit ends.
    /// </summary>
    Sleep,

    /// <summary>
    /// Can't move, but still hits whoever is in reach.
    /// </summary>
    Root,

    /// <summary>
    /// Runs from whoever scared it.
    /// </summary>
    Flee,

    /// <summary>
    /// Shoved away from whoever hit it, then stunned until the time runs out.
    /// </summary>
    Knockback
}

/// <summary>
/// Makes a hostile ("red") world NPC a mob: hit points, the <see cref="MobBrain"/>'s aggro, chase and attacks
/// carried out on the NPC, and death, kill rewards and respawn. Attached by WorldZone to any world NPC
/// whose disposition is hostile.
/// </summary>
public sealed class NpcCombat
{
    private const int ThinkIntervalMs = 250;
    private const int ChaseRepathMs = 500;
    private const float ChaseRepathDistance = 1.5f;
    private const float DirectChaseDistance = 12f;
    private const int IdleCharacterState = 1;
    private const int KnockedOutCharacterState = 0x80;
    private const int FleeRepathMs = 1000;
    private const float FleeStepDistance = 10f;

    // How long a knockback shove takes, whatever its distance.
    private const int KnockbackMoveMs = 400;

    private readonly object _lock = new();
    private readonly Npc _npc;
    private readonly IRewardManager _rewardManager;
    private readonly MobBrain _brain;

    // Who hurt it this life, for kill rewards.
    private readonly HashSet<ulong> _attackers = [];

    private long _nextThinkAt;
    private long _nextChaseAt;
    private Vector3 _chaseGoal;
    private bool _walkingHome;
    private long _despawnAt;
    private long _respawnAt;

    private MobControl _control;
    private long _controlUntil;
    private ulong _controlSourceGuid;
    private int _controlEffectTagId;
    private float _speedBeforeKnockback;

    private readonly List<DamageOverTime> _damageOverTime = [];

    private sealed class DamageOverTime(ulong attackerGuid, int damagePerTick, int intervalMs, int hitEffectId)
    {
        public ulong AttackerGuid { get; } = attackerGuid;
        public int DamagePerTick { get; } = damagePerTick;
        public int IntervalMs { get; } = intervalMs;
        public int HitEffectId { get; } = hitEffectId;
        public int TicksLeft { get; set; }
        public long NextTickAt { get; set; }
    }

    public NpcCombat(Npc npc, MobDefinition definition, IRewardManager rewardManager)
    {
        _npc = npc;
        _rewardManager = rewardManager;
        _brain = new MobBrain(definition);

        Hitpoints = definition.MaxHitpoints;
    }

    public MobDefinition Definition
    {
        get => _brain.Definition;
        set
        {
            lock (_lock)
            {
                var fraction = (float)Hitpoints / MaxHitpoints;

                _brain.Definition = value;

                if (!IsDead)
                    Hitpoints = Math.Max(1, (int)MathF.Round(fraction * MaxHitpoints));
            }

            SendHitpoints();
        }
    }

    public int Hitpoints { get; private set; }

    public int MaxHitpoints => Definition.MaxHitpoints;

    public MobState State => _brain.State;

    public bool IsDead => _brain.State == MobState.Dead;

    /// <summary>
    /// Dead and gone from view until it respawns.
    /// </summary>
    public bool IsDespawned { get; private set; }

    public ulong TargetGuid => _brain.TargetGuid;

    public MobControl Control
    {
        get
        {
            lock (_lock)
                return _control;
        }
    }

    public static bool CanBeAttacked(Player player, IEntity mob) =>
        player.Visible && !player.IsDead && !player.IsInGodMode && player.CanFight && ReferenceEquals(player.Zone, mob.Zone);

    public void Update()
    {
        var now = Environment.TickCount64;

        if (IsDead)
        {
            UpdateCorpse(now);
            return;
        }

        TickDamageOverTime(now);

        if (IsDead || now < _nextThinkAt)
            return;

        _nextThinkAt = now + ThinkIntervalMs;

        var control = UpdateControl(now);

        if (control is MobControl.Stun or MobControl.Sleep or MobControl.Knockback)
            return;

        if (control == MobControl.Flee)
        {
            Flee(now);
            return;
        }

        var candidates = _npc.VisiblePlayers.Values
            .Select(player => new MobCandidate(player.Guid, player.Position.ToVector3(), CanBeAttacked(player, _npc)))
            .ToList();

        MobAction action;

        lock (_lock)
        {
            if (IsDead)
                return;

            action = _brain.Think(now, _npc.Position.ToVector3(), _npc.SpawnPosition.ToVector3(), candidates);
        }

        Player? target = null;

        if (action.TargetGuid != 0)
            _npc.VisiblePlayers.TryGetValue(action.TargetGuid, out target);

        if (action.Engaged)
            target?.OnAttackedBy(_npc);

        switch (action.Type)
        {
            // Rooted, it can only hit what's already in reach.
            case MobActionType.Chase when control == MobControl.Root:
                _npc.StopMoving();
                break;

            case MobActionType.Chase:
                Chase(now, action.Destination);
                break;

            case MobActionType.Attack when target is not null:
                Attack(target);
                break;

            case MobActionType.Return:
                if (!_walkingHome && control != MobControl.Root)
                {
                    _walkingHome = true;
                    _npc.MoveTo(action.Destination);
                }
                break;

            case MobActionType.Reset:
                _walkingHome = false;
                _npc.StopMoving();
                _npc.UpdatePosition(_npc.Position, _npc.SpawnRotation);

                lock (_lock)
                    Hitpoints = MaxHitpoints;

                SendHitpoints();
                break;
        }
    }

    /// <summary>
    /// A player clicked it: it turns on them (and they get their combat job out).
    /// </summary>
    public void OnEngagedBy(Player player)
    {
        if (!CanBeAttacked(player, _npc))
            return;

        lock (_lock)
            _brain.AddThreat(player.Guid, 0);

        player.OnAttackedBy(_npc);
    }

    /// <summary>
    /// Damage from a player's ability. Returns what it took (0 if it was already dead).
    /// </summary>
    public int TakeDamage(Player attacker, int damage)
    {
        if (damage <= 0)
            return 0;

        bool killed;

        lock (_lock)
        {
            if (IsDead || _brain.State == MobState.Returning)
                return 0;

            damage = Math.Min(damage, Hitpoints);
            Hitpoints -= damage;

            _attackers.Add(attacker.Guid);
            _brain.AddThreat(attacker.Guid, damage);

            killed = Hitpoints <= 0;

            if (killed)
                Die();
        }

        // Any hit wakes a sleeper, and a dead mob is let go of.
        if (killed || Control == MobControl.Sleep)
            EndControl();

        // Bar, floating number (for the attacker) and flinch in one.
        SendToVisible(new PlayerUpdatePacketHitPointModification
        {
            SourceGuid = attacker.Guid,
            TargetGuid = _npc.Guid,
            ShowDelta = true,
            MaxHitpoints = MaxHitpoints,
            CurrentHitpoints = Hitpoints,
            Delta = -damage
        });

        if (killed)
        {
            SendToVisible(new PlayerUpdatePacketUpdateCharacterState { Guid = _npc.Guid, State = KnockedOutCharacterState });

            if (Definition.DeathAnimationId != 0)
                _npc.PlayAnimation(Definition.DeathAnimationId, loop: false);

            GrantKillRewards();
        }
        else if (Definition.HitAnimationId != 0)
            _npc.PlayAnimation(Definition.HitAnimationId, loop: false);

        return damage;
    }

    /// <summary>
    /// Holds it with an orb or grenade for <paramref name="durationMs"/>, replacing any hold it was under.
    /// <paramref name="effectId"/> shows on it until then; a knockback shoves it <paramref name="distance"/>
    /// away from <paramref name="source"/> first. Either way it turns on <paramref name="source"/>.
    /// </summary>
    public bool ApplyControl(Player source, MobControl control, int durationMs, int effectId = 0, float distance = 0f)
    {
        if (control == MobControl.None || durationMs <= 0)
            return false;

        lock (_lock)
        {
            if (IsDead || _brain.State == MobState.Returning)
                return false;

            _brain.AddThreat(source.Guid, 0);
        }

        EndControl();

        var tagId = effectId != 0 ? _npc.AddEffectTag(effectId) : 0;

        lock (_lock)
        {
            _control = control;
            _controlUntil = Environment.TickCount64 + durationMs;
            _controlSourceGuid = source.Guid;
            _controlEffectTagId = tagId;
        }

        _walkingHome = false;
        _npc.StopMoving();

        if (control == MobControl.Knockback && distance > 0f)
            ShoveAwayFrom(source.Position.ToVector3(), distance);

        return true;
    }

    /// <summary>
    /// <paramref name="ticks"/> hits of <paramref name="damagePerTick"/> from <paramref name="attacker"/>, one
    /// every <paramref name="intervalMs"/>, each playing <paramref name="hitEffectId"/>.
    /// </summary>
    public bool AddDamageOverTime(Player attacker, int damagePerTick, int ticks, int intervalMs, int hitEffectId = 0)
    {
        if (damagePerTick <= 0 || ticks <= 0 || intervalMs <= 0)
            return false;

        lock (_lock)
        {
            if (IsDead || _brain.State == MobState.Returning)
                return false;

            _brain.AddThreat(attacker.Guid, 0);

            _damageOverTime.Add(new DamageOverTime(attacker.Guid, damagePerTick, intervalMs, hitEffectId)
            {
                TicksLeft = ticks,
                NextTickAt = Environment.TickCount64 + intervalMs
            });
        }

        return true;
    }

    /// <summary>
    /// No longer hostile: drop the fight and stand where it is, back on full health.
    /// </summary>
    public void Detach()
    {
        EndControl();

        lock (_lock)
        {
            _brain.Revive();
            _damageOverTime.Clear();
            Hitpoints = MaxHitpoints;
        }

        _npc.StopMoving();

        if (IsDespawned)
            Show();
    }

    public PlayerUpdatePacketUpdateHitpoints GetHitpointsPacket() => new()
    {
        Guid = _npc.Guid,
        HealthPercent = PlayerUpdatePacketUpdateHitpoints.GetHealthPercent(Hitpoints, MaxHitpoints),
        MaxHitpoints = MaxHitpoints,
        CurrentHitpoints = Hitpoints
    };

    private void SendHitpoints() => SendToVisible(GetHitpointsPacket());

    private void SendToVisible(ISerializablePacket serializablePacket)
    {
        if (_npc.VisiblePlayers.IsEmpty)
            return;

        var data = Player.SerializeTunneled(serializablePacket);

        foreach (var visiblePlayer in _npc.VisiblePlayers.Values)
            visiblePlayer.SendSerialized(data);
    }

    /// <summary>
    /// The hold it's under, ending it once its time is up.
    /// </summary>
    private MobControl UpdateControl(long now)
    {
        lock (_lock)
        {
            if (_control == MobControl.None || now < _controlUntil)
                return _control;
        }

        EndControl();

        return MobControl.None;
    }

    private void EndControl()
    {
        MobControl control;
        int tagId;

        lock (_lock)
        {
            control = _control;
            tagId = _controlEffectTagId;

            _control = MobControl.None;
            _controlEffectTagId = 0;
        }

        if (control == MobControl.None)
            return;

        if (control == MobControl.Knockback)
        {
            _npc.StopMoving();
            _npc.Speed = _speedBeforeKnockback;
        }

        if (tagId != 0)
            _npc.RemoveEffectTag(tagId);
    }

    private void ShoveAwayFrom(Vector3 source, float distance)
    {
        var position = _npc.Position.ToVector3();
        var away = position - source;
        away.Y = 0f;
        away = away.LengthSquared() > 0.0001f ? Vector3.Normalize(away) : Vector3.UnitX;

        // Fast enough to look like a shove rather than a walk; EndControl puts its speed back.
        _speedBeforeKnockback = _npc.Speed;
        _npc.Speed = MathF.Max(_npc.Speed, distance * 1000f / KnockbackMoveMs);
        _npc.MoveTo(position + away * distance, direct: true);
    }

    private void Flee(long now)
    {
        if (now < _nextChaseAt)
            return;

        _nextChaseAt = now + FleeRepathMs;

        ulong sourceGuid;

        lock (_lock)
            sourceGuid = _controlSourceGuid;

        if (!_npc.VisiblePlayers.TryGetValue(sourceGuid, out var source))
            return;

        var position = _npc.Position.ToVector3();
        var away = position - source.Position.ToVector3();
        away.Y = 0f;
        away = away.LengthSquared() > 0.0001f ? Vector3.Normalize(away) : Vector3.UnitX;

        _npc.MoveTo(position + away * FleeStepDistance);
    }

    private void TickDamageOverTime(long now)
    {
        List<(ulong AttackerGuid, int Damage, int HitEffectId)>? due = null;

        lock (_lock)
        {
            for (var i = _damageOverTime.Count - 1; i >= 0; i--)
            {
                var damageOverTime = _damageOverTime[i];

                while (damageOverTime.TicksLeft > 0 && damageOverTime.NextTickAt <= now)
                {
                    damageOverTime.TicksLeft--;
                    damageOverTime.NextTickAt += damageOverTime.IntervalMs;

                    (due ??= []).Add((damageOverTime.AttackerGuid, damageOverTime.DamagePerTick, damageOverTime.HitEffectId));
                }

                if (damageOverTime.TicksLeft == 0)
                    _damageOverTime.RemoveAt(i);
            }
        }

        if (due is null)
            return;

        foreach (var (attackerGuid, damage, hitEffectId) in due)
        {
            // Whoever threw it logged off or left: the rest fizzles out.
            if (!_npc.Zone.TryGetPlayer(attackerGuid, out var attacker))
                continue;

            if (TakeDamage(attacker, damage) > 0 && hitEffectId != 0)
                _npc.PlayEffect(hitEffectId);
        }
    }

    private void Chase(long now, Vector3 targetPosition)
    {
        _walkingHome = false;

        // Stop just inside attack range rather than on top of the player.
        var position = _npc.Position.ToVector3();
        var toTarget = targetPosition - position;
        var distance = toTarget.Length();
        var stopShort = MathF.Min(Definition.AttackRange * 0.75f, distance);
        var goal = distance > 0.01f ? targetPosition - toTarget / distance * stopShort : targetPosition;

        if (now < _nextChaseAt && Vector3.DistanceSquared(goal, _chaseGoal) < ChaseRepathDistance * ChaseRepathDistance)
            return;

        _nextChaseAt = now + ChaseRepathMs;
        _chaseGoal = goal;

        _npc.MoveTo(goal, direct: distance <= DirectChaseDistance);
    }

    private void Attack(Player target)
    {
        _walkingHome = false;

        _npc.StopMoving();
        _npc.FaceTowards(target.Position);

        if (Definition.AttackAnimationId != 0)
            _npc.PlayAnimation(Definition.AttackAnimationId, loop: false);

        var damage = CombatRules.RollDamage(Definition.Damage, Definition.DamageVariance, Random.Shared.NextDouble());

        if (target.TakeDamage(damage, _npc) > 0 && Definition.HitEffectId != 0)
        {
            target.SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect
            {
                Guid = target.Guid,
                CompositeEffectId = Definition.HitEffectId,
                Position = target.Position
            }, true);
        }
    }

    private void Die()
    {
        _brain.Kill();
        _damageOverTime.Clear();
        _walkingHome = false;
        _npc.StopMoving();

        var now = Environment.TickCount64;

        _despawnAt = now + Definition.CorpseMs;
        _respawnAt = _despawnAt + Definition.RespawnSeconds * 1000L;
    }

    private void UpdateCorpse(long now)
    {
        if (!IsDespawned && now >= _despawnAt)
            Hide();
        else if (IsDespawned && now >= _respawnAt)
            Respawn();
    }

    private void Hide()
    {
        IsDespawned = true;

        foreach (var visiblePlayer in _npc.VisiblePlayers.Values)
            visiblePlayer.OnRemoveVisibleNpcs([_npc]);
    }

    private void Respawn()
    {
        lock (_lock)
        {
            _brain.Revive();
            _attackers.Clear();
            Hitpoints = MaxHitpoints;
        }

        _npc.UpdatePosition(_npc.SpawnPosition, _npc.SpawnRotation);

        Show();
    }

    private void Show()
    {
        IsDespawned = false;

        foreach (var visiblePlayer in _npc.VisiblePlayers.Values)
            visiblePlayer.OnAddVisibleNpcs([_npc]);
    }

    /// <summary>
    /// Everyone who hurt it and is still around gets the mob's coins and stars, and a roll on its reward table.
    /// </summary>
    private void GrantKillRewards()
    {
        ulong[] attackers;

        lock (_lock)
        {
            attackers = [.. _attackers];
            _attackers.Clear();
        }

        foreach (var guid in attackers)
        {
            if (!_npc.Zone.TryGetPlayer(guid, out var player))
                continue;

            var coins = player.ApplyCoinBoost(Definition.Coins);
            var experience = player.ApplyExperienceBoost(Definition.Experience);

            if (coins > 0)
                _rewardManager.TryGrantCurrency(player, CurrencyType.Coins, coins);

            if (coins > 0 || experience > 0)
            {
                var rewardBundlePacket = new RewardBundlePacket();

                rewardBundlePacket.RewardBundle.Coins = coins;
                rewardBundlePacket.RewardBundle.Experience = experience;
                rewardBundlePacket.RewardBundle.Multiplier = 1f;
                rewardBundlePacket.RewardBundle.IconId = -1;
                rewardBundlePacket.RewardBundle.NameId = -1;

                player.SendTunneled(rewardBundlePacket);
            }

            _rewardManager.TryGrantExperience(player, experience);

            if (!string.IsNullOrWhiteSpace(Definition.RewardTable) && Random.Shared.NextSingle() < Definition.RewardChance &&
                _rewardManager.TryRollReward(Definition.RewardTable, out var drop) && drop is not null)
                _rewardManager.TryGrantReward(player, drop, _npc.Guid);

            player.OnKilled(_npc);
        }
    }
}
