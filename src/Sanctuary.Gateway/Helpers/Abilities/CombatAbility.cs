using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Helpers.Abilities;

/// <summary>
/// The combat job toolbar's basic and special attacks (CombatJobs.json / CombatAbilities.json, as
/// Player.SendToolbar lays them out): damage to the targeted mob, or every mob around it for AoeDamage.
/// </summary>
public sealed class CombatAbility(AbilityServices services)
{
    public const int ActionBarId = 1;

    private const string AoeDamageEffectType = "AoeDamage";
    private const float DamageVariance = 0.1f;

    // Client and server positions drift apart a little while both move.
    private const float ReachSlack = 2f;

    public bool Matches(Player player, AbilityPacketClientRequestStartAbility packet) =>
        packet.Data.Id == ActionBarId && player.TryGetToolbarAbility(packet.Data.Slot, out _, out _);

    public bool HandleAbility(Player player, AbilityPacketClientRequestStartAbility packet)
    {
        if (player.IsDead || !player.TryGetToolbarAbility(packet.Data.Slot, out var kit, out var ability))
            return ConsumableAbility.SendFailure(player);

        var now = Environment.TickCount64;
        var isBasic = packet.Data.Slot == Player.BasicToolbarSlot;

        if (isBasic ? now < player.NextBasicAttackAt : player.Energy < ability.EnergyCost)
            return ConsumableAbility.SendFailure(player);

        var reach = (isBasic && kit.BasicAutoTargetReach > 0 ? kit.BasicAutoTargetReach : kit.AutoTargetReach) + ReachSlack;
        var target = FindTarget(player, packet.Target == 1 ? 0 : packet.Guid, reach);

        if (target is null)
            return ConsumableAbility.SendFailure(player);

        if (isBasic)
            player.NextBasicAttackAt = now + kit.BasicRecastMs;
        else
            player.Energy -= ability.EnergyCost;

        player.EnterCombat();

        player.SendTunneledToVisible(new AbilityPacketStartCasting
        {
            CasterGuid = player.Guid,
            Unused = target.Guid,
            CompositeEffectId = ability.CastEffectId,
            Animation = ability.AnimationId,
            AbilityId = isBasic ? kit.BasicSlotDefId : kit.SpecialSlotDefId
        }, true);

        var targets = ability.EffectType.Equals(AoeDamageEffectType, StringComparison.OrdinalIgnoreCase) && ability.AoeRadius > 0f
            ? FindMobsAround(player, target.Position.ToVector3(), ability.AoeRadius)
            : [target];

        // The job's rank scales the ability; attack foods (+20 EquippedMeleeWeaponDamage) add to every hit.
        var damagePerHit = CombatRules.ScaleDamageForRank(ability.Damage, player.ActiveProfile.Rank)
            + (int)MathF.Round(player.GetStatBonus(CharacterStatId.EquippedMeleeWeaponDamage));

        foreach (var mob in targets)
        {
            var damage = 0;

            for (var hit = 0; hit < Math.Max(1, ability.HitCount); hit++)
                damage += CombatRules.RollDamage(damagePerHit, DamageVariance, Random.Shared.NextDouble());

            if (mob.Combat!.TakeDamage(player, damage) > 0 && ability.HitEffectId != 0)
                mob.PlayEffect(ability.HitEffectId);
        }

        if (ability.CasterEndEffectId != 0)
        {
            player.SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect
            {
                Guid = player.Guid,
                CompositeEffectId = ability.CasterEndEffectId,
                Position = player.Position
            }, true);
        }

        services.Logger.LogTrace("{player} used combat ability {ability} on {count} mob(s).", player.Guid, ability.Id, targets.Count);

        return true;
    }

    /// <summary>
    /// The clicked mob if it's alive and in reach, otherwise the nearest one in reach.
    /// </summary>
    internal static Npc? FindTarget(Player player, ulong targetGuid, float reach)
    {
        if (targetGuid != 0 && player.VisibleNpcs.TryGetValue(targetGuid, out var clicked) && IsAliveMob(clicked) &&
            IsWithin(player.Position.ToVector3(), clicked.Position.ToVector3(), reach))
            return clicked;

        return FindMobsAround(player, player.Position.ToVector3(), reach)
            .OrderBy(mob => Vector3.DistanceSquared(player.Position.ToVector3(), mob.Position.ToVector3()))
            .FirstOrDefault();
    }

    internal static List<Npc> FindMobsAround(Player player, Vector3 center, float radius) =>
        player.VisibleNpcs.Values
            .Where(npc => IsAliveMob(npc) && ReferenceEquals(npc.Zone, player.Zone) && IsWithin(center, npc.Position.ToVector3(), radius))
            .ToList();

    private static bool IsAliveMob(Npc npc) => npc.Combat is { IsDead: false };

    private static bool IsWithin(Vector3 position, Vector3 other, float radius) =>
        Vector3.DistanceSquared(position, other) <= radius * radius;
}
