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
// grenades also damage, stun, root, scare or knock back mobs.
public sealed class ItemAbility(AbilityServices services) : ConsumableAbility(services)
{
    private const float DamageVariance = 0.1f;

    public override bool Matches(ClientItemDefinition itemDefinition) =>
        _resourceManager.ItemAbilities.ContainsKey(itemDefinition.ActivatableAbilityId);

    public override bool HandleAbility(Player player, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition)
    {
        // Nothing to drink while defeated, so the potion isn't used up for nothing.
        if (player.IsDead || player.IsItemOnCooldown(itemDefinition.Id) ||
            !_resourceManager.ItemAbilities.TryGetValue(itemDefinition.ActivatableAbilityId, out var ability))
            return SendFailure(player);

        List<Npc> mobs = [];

        // Likewise an orb with no mob in reach is kept.
        if (ability.Attack is { } attack && (mobs = FindMobs(player, packet, attack)).Count == 0)
            return SendFailure(player);

        var cooldownMs = ClampCooldown(ability.CooldownMs);

        player.StartItemCooldown(itemDefinition.Id, cooldownMs);

        ItemAbilityHelper.Apply(player, ability, itemDefinition.NameId, itemDefinition.Icon.Id);

        if (ability.Attack is not null)
            AttackMobs(player, ability.Attack, mobs);

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
