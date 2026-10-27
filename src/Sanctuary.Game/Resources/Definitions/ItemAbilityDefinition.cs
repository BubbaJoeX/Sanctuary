using System.Collections.Generic;

using Sanctuary.Game.Entities;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Resources.Definitions;

/// <summary>
/// What an item's ActivatableAbilityId does when used: potions, scrolls, buff foods, combat orbs and grenades.
/// Generated from client data by ClientMods/extract_item_abilities.py.
/// </summary>
public class ItemAbilityDefinition
{
    public int AbilityId { get; set; }

    /// <summary>
    /// Buffs in the same group replace each other instead of stacking (e.g. two coin scrolls).
    /// </summary>
    public string? Group { get; set; }

    public int CooldownMs { get; set; }

    /// <summary>
    /// How long the buff (stats, speed, scale, aura) lasts. 0 for instant abilities.
    /// </summary>
    public int DurationMs { get; set; }

    /// <summary>
    /// Composite effect played once on use.
    /// </summary>
    public int CastEffectId { get; set; }

    /// <summary>
    /// Composite effect shown on the player for as long as the buff lasts.
    /// </summary>
    public int AuraEffectId { get; set; }

    /// <summary>
    /// Buff bar icon. 0 uses the item's own icon.
    /// </summary>
    public int BuffIconId { get; set; }

    public ItemAbilityRestoreDefinition? Health { get; set; }
    public ItemAbilityRestoreDefinition? Energy { get; set; }

    public float MovementSpeedBonus { get; set; }
    public float Scale { get; set; }

    /// <summary>
    /// Temporary appearance for <see cref="DurationMs"/> (illusion foods and masks), with <see cref="CastEffectId"/> as its poof.
    /// </summary>
    public int ModelId { get; set; }

    public List<ItemAbilityStatDefinition> Stats { get; set; } = [];

    /// <summary>
    /// Every other player this close gets the ability too (shared potions). 0 for the user alone.
    /// </summary>
    public float ShareRadius { get; set; }

    /// <summary>
    /// What a combat orb or grenade does to mobs. The item isn't used up when no mob is in reach,
    /// unless it also changes the user (see <see cref="AffectsUser"/>).
    /// </summary>
    public ItemAbilityAttackDefinition? Attack { get; set; }

    /// <summary>
    /// Whether it does anything to the user besides the cast effect: a restore, a new look or a timed buff.
    /// </summary>
    public bool AffectsUser => ModelId != 0 || DurationMs > 0 || Health is not null || Energy is not null;
}

public class ItemAbilityAttackDefinition
{
    /// <summary>
    /// Dealt to each mob hit, all at once or spread over <see cref="DamageOverMs"/>.
    /// </summary>
    public int Damage { get; set; }

    public int DamageOverMs { get; set; }

    public int DamageIntervalMs { get; set; } = 1000;

    /// <summary>
    /// How far away the clicked (or nearest) mob can be.
    /// </summary>
    public float Range { get; set; } = 20f;

    /// <summary>
    /// Also hits every mob this close to the target, or to the player for <see cref="AroundCaster"/> (grenades).
    /// 0 hits the target alone.
    /// </summary>
    public float Radius { get; set; }

    public bool AroundCaster { get; set; }

    /// <summary>
    /// The most mobs one use hits, nearest first. 0 for no limit.
    /// </summary>
    public int MaxTargets { get; set; }

    public MobControl Control { get; set; }

    public int ControlMs { get; set; }

    public float KnockbackDistance { get; set; }

    /// <summary>
    /// Played on each mob it hits.
    /// </summary>
    public int HitEffectId { get; set; }

    /// <summary>
    /// Shown on each mob for as long as <see cref="Control"/> holds it.
    /// </summary>
    public int ControlEffectId { get; set; }
}

public class ItemAbilityRestoreDefinition
{
    public int Amount { get; set; }

    /// <summary>
    /// Percent of the maximum, added to <see cref="Amount"/>.
    /// </summary>
    public float Percent { get; set; }

    /// <summary>
    /// Spreads the restore over this long, one tick per <see cref="IntervalMs"/>. 0 restores it all at once.
    /// </summary>
    public int OverMs { get; set; }

    public int IntervalMs { get; set; } = 1000;
}

public class ItemAbilityStatDefinition
{
    public CharacterStatId Stat { get; set; }
    public float Value { get; set; }
}
