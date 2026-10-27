using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Helpers.Abilities;

// Potions, scrolls and buff foods from ItemAbilities.json: heals and energy (instant or over time),
// coin/star boosts and other stat buffs, run speed, size, auras and fireworks. Combat orbs and
// grenades also damage, stun, root, scare or knock back mobs, and the Resurrection Scroll stands a
// knocked-out ally back up.
public sealed class ItemAbility(AbilityServices services) : ConsumableAbility(services)
{
    private const float DamageVariance = 0.1f;

    public override bool Matches(ClientItemDefinition itemDefinition) =>
        _resourceManager.ItemAbilities.ContainsKey(itemDefinition.ActivatableAbilityId);

    public override bool HandleAbility(Player player, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition)
    {
        // Nothing to drink while knocked out, so the potion isn't used up for nothing.
        if (player.IsDead)
            return SendFailure(player, KnockedOutStringId);

        if (player.IsItemOnCooldown(itemDefinition.Id) ||
            !_resourceManager.ItemAbilities.TryGetValue(itemDefinition.ActivatableAbilityId, out var ability))
            return SendFailure(player);

        List<Npc> mobs = [];

        // Likewise an orb with no mob in reach is kept, unless it changes the user too (a scare potion still transforms them).
        if (ability.Attack is { } attack && (mobs = FindMobs(player, packet, attack)).Count == 0 && !ability.AffectsUser)
            return SendFailure(player);

        // And a Resurrection Scroll with nobody to revive.
        if (ability.Revive is { } revive && !TryReviveAlly(player, packet, revive))
            return SendFailure(player);

        var cooldownMs = ClampCooldown(ability.CooldownMs);

        player.StartItemCooldown(itemDefinition.Id, cooldownMs);

        ItemAbilityHelper.Apply(player, ability, itemDefinition.NameId, itemDefinition.Icon.Id);

        foreach (var pal in FindPlayersAround(player, ability.ShareRadius))
            ItemAbilityHelper.Apply(pal, ability, itemDefinition.NameId, itemDefinition.Icon.Id);

        if (mobs.Count > 0)
            AttackMobs(player, ability.Attack!, mobs);

        if (ability.RewardTable is not null && _rewardManager.TryRollReward(ability.RewardTable, out var drop) && drop is not null)
            _rewardManager.TryGrantReward(player, drop);

        FinishActivation(player, clientItem, itemDefinition, slot, cooldownMs, IconTintId(clientItem, itemDefinition.Icon.TintId));

        return true;
    }

    /// <summary>
    /// The clicked (or nearest) mob in range and those around it, or the mobs around the player for a
    /// grenade; nearest first, at most <see cref="ItemAbilityAttackDefinition.MaxTargets"/>.
    /// </summary>
    private static List<Npc> FindMobs(Player player, AbilityPacketClientRequestStartAbility packet, ItemAbilityAttackDefinition attack)
    {
        Vector3 center;
        List<Npc> mobs;

        if (attack.AroundCaster)
        {
            center = player.Position.ToVector3();
            mobs = CombatAbility.FindMobsAround(player, center, attack.Radius);
        }
        else
        {
            var target = CombatAbility.FindTarget(player, packet.Target == 1 ? 0 : packet.Guid, attack.Range);

            if (target is null)
                return [];

            center = target.Position.ToVector3();
            mobs = attack.Radius > 0f ? CombatAbility.FindMobsAround(player, center, attack.Radius) : [target];
        }

        var nearestFirst = mobs.OrderBy(mob => Vector3.DistanceSquared(center, mob.Position.ToVector3()));

        return (attack.MaxTargets > 0 ? nearestFirst.Take(attack.MaxTargets) : nearestFirst).ToList();
    }

    /// <summary>
    /// The other standing players within <paramref name="radius"/> of the user, for a shared potion.
    /// </summary>
    private static IEnumerable<Player> FindPlayersAround(Player player, float radius)
    {
        if (radius <= 0f)
            return [];

        var center = player.Position.ToVector3();

        return player.VisiblePlayers.Values
            .Where(other => !other.IsDead && Vector3.DistanceSquared(center, other.Position.ToVector3()) <= radius * radius)
            .ToList();
    }

    /// <summary>
    /// Stands up the clicked (or nearest) knocked-out player in range, see <see cref="CombatRules.PickReviveTarget"/>.
    /// Otherwise tells the user why not.
    /// </summary>
    private static bool TryReviveAlly(Player player, AbilityPacketClientRequestStartAbility packet, ItemAbilityReviveDefinition revive)
    {
        var players = player.VisiblePlayers.Values
            .Where(other => ReferenceEquals(other.Zone, player.Zone))
            .ToDictionary(other => other.Guid);

        var candidates = players.Values.Select(other => new ReviveCandidate(other.Guid, other.Position.ToVector3(), other.IsKnockedOut)).ToList();
        var clickedGuid = packet.Target == 1 ? 0 : packet.Guid;

        var failure = CombatRules.PickReviveTarget(player.Guid, player.Position.ToVector3(), clickedGuid, candidates, revive.Range, out var allyGuid);

        if (failure != ReviveFailure.None)
        {
            ChatHelper.SendSystemMessage(player, failure switch
            {
                ReviveFailure.NotKnockedOut => $"{players[clickedGuid].Name.FullName} isn't knocked out.",
                ReviveFailure.TooFar => $"{players[clickedGuid].Name.FullName} is too far away. Get within {revive.Range:0} m to revive them.",
                _ => $"There's no knocked-out player within {revive.Range:0} m to revive."
            });

            return false;
        }

        var ally = players[allyGuid];

        // They can get up at the zone's start in the meantime.
        if (!ally.Revive(CombatRules.ReviveHitpoints(ally.MaxHitpoints, revive.HealthPercent), player.Guid))
        {
            ChatHelper.SendSystemMessage(player, $"{ally.Name.FullName} already got up.");

            return false;
        }

        PlayEffect(ally, revive.EffectId);

        ChatHelper.SendSystemMessage(ally, $"{player.Name.FullName} revived you!");
        ChatHelper.SendSystemMessage(player, $"You revived {ally.Name.FullName}.");

        return true;
    }

    private static void AttackMobs(Player player, ItemAbilityAttackDefinition attack, List<Npc> mobs)
    {
        player.EnterCombat();

        foreach (var mob in mobs)
        {
            var combat = mob.Combat!;
            var hit = false;

            if (attack.Damage > 0 && attack.DamageOverMs > 0)
            {
                var ticks = attack.DamageOverMs / attack.DamageIntervalMs;

                combat.AddDamageOverTime(player, Math.Max(1, attack.Damage / ticks), ticks, attack.DamageIntervalMs, attack.HitEffectId);
            }
            else if (attack.Damage > 0)
                hit = combat.TakeDamage(player, CombatRules.RollDamage(attack.Damage, DamageVariance, Random.Shared.NextDouble())) > 0;

            // After the damage, so a sleep isn't woken by its own orb.
            if (attack.Control != MobControl.None)
                hit |= combat.ApplyControl(player, attack.Control, attack.ControlMs, attack.ControlEffectId, attack.KnockbackDistance);

            if (hit && attack.HitEffectId != 0)
                mob.PlayEffect(attack.HitEffectId);
        }
    }
}
