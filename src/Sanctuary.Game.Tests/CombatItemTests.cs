using System;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions.Combat;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class CombatItemTests
{
    private static readonly string Resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Resources"));

    private static (Npc Npc, NpcCombat Combat, Player Player) CreateMob()
    {
        var npc = new Npc(null!);
        var combat = new NpcCombat(npc, new MobDefinition { Key = "test", MaxHitpoints = 1000 }, null!);

        npc.Combat = combat;

        return (npc, combat, new Player(null!, null!, null!, null!));
    }

    [TestMethod]
    public void ApplyControl_HitWakesSleeperButNotStunned()
    {
        var (_, combat, player) = CreateMob();

        Assert.IsTrue(combat.ApplyControl(player, MobControl.Sleep, 5000));
        Assert.AreEqual(MobControl.Sleep, combat.Control);
        Assert.AreEqual(MobState.Fighting, combat.State, "It turns on whoever put it to sleep.");

        combat.TakeDamage(player, 10);
        Assert.AreEqual(MobControl.None, combat.Control);

        Assert.IsTrue(combat.ApplyControl(player, MobControl.Stun, 5000));
        combat.TakeDamage(player, 10);
        Assert.AreEqual(MobControl.Stun, combat.Control);
    }

    [TestMethod]
    public void ApplyControl_KnockbackGivesSpeedBackWhenItEnds()
    {
        var (npc, combat, player) = CreateMob();
        var speed = npc.Speed;

        Assert.IsTrue(combat.ApplyControl(player, MobControl.Knockback, 2000, distance: 10f));
        Assert.IsTrue(npc.Speed > speed, "The shove is faster than a walk.");
        Assert.IsTrue(npc.IsMoving);

        combat.Detach();

        Assert.AreEqual(speed, npc.Speed);
        Assert.AreEqual(MobControl.None, combat.Control);
    }

    [TestMethod]
    public void ApplyControl_IgnoresDeadMobsAndEmptyHolds()
    {
        var (_, combat, player) = CreateMob();

        Assert.IsFalse(combat.ApplyControl(player, MobControl.None, 5000));
        Assert.IsFalse(combat.ApplyControl(player, MobControl.Stun, 0));
        Assert.IsFalse(combat.AddDamageOverTime(player, 0, 5, 1000));
        Assert.AreEqual(MobControl.None, combat.Control);
    }

    [TestMethod]
    public void ItemAbilitiesJson_OrbsAndGrenades()
    {
        var abilities = new ItemAbilityDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(abilities.Load(Path.Combine(Resources, "ItemAbilities.json")));

        Assert.IsTrue(abilities.TryGetValue(1644, out var fragOrb), "Frag Orb");
        Assert.AreEqual(600, fragOrb.Attack!.Damage);
        Assert.AreEqual(0f, fragOrb.Attack.Radius);

        Assert.IsTrue(abilities.TryGetValue(3954, out var frostGrenade), "Frost Grenade");
        Assert.IsTrue(frostGrenade.Attack!.AroundCaster);
        Assert.AreEqual(5, frostGrenade.Attack.MaxTargets);
        Assert.AreEqual(MobControl.Stun, frostGrenade.Attack.Control);
        Assert.AreEqual(5000, frostGrenade.Attack.ControlMs);

        Assert.IsTrue(abilities.TryGetValue(3101, out var blastOrb), "Blast Orb");
        Assert.AreEqual(MobControl.Knockback, blastOrb.Attack!.Control);
        Assert.IsTrue(blastOrb.Attack.KnockbackDistance > 0);

        var attacks = abilities.Values.Count(ability => ability.Attack is not null);
        Assert.IsTrue(attacks >= 40, $"Only {attacks} combat orbs and grenades.");
    }

    [TestMethod]
    public void ItemAbilitiesJson_ScaresAndSharedPotions()
    {
        var abilities = new ItemAbilityDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(abilities.Load(Path.Combine(Resources, "ItemAbilities.json")));

        // A scare transforms the user even with no mob around, so it isn't kept back like an orb.
        Assert.IsTrue(abilities.TryGetValue(4078, out var hologramSpores), "Hologram Spores");
        Assert.AreEqual(MobControl.Flee, hologramSpores.Attack!.Control);
        Assert.AreEqual(30000, hologramSpores.Attack.ControlMs);
        Assert.AreNotEqual(0, hologramSpores.ModelId);
        Assert.IsTrue(hologramSpores.AffectsUser);
        Assert.IsTrue(hologramSpores.CooldownMs >= hologramSpores.Attack.ControlMs, "Ready again only once the fright is over.");

        Assert.IsTrue(abilities.TryGetValue(1644, out var fragOrb), "Frag Orb");
        Assert.IsFalse(fragOrb.AffectsUser);

        Assert.IsTrue(abilities.TryGetValue(4722, out var sharedHealth), "Shared Health Potion");
        Assert.IsNotNull(sharedHealth.Health);
        Assert.IsTrue(sharedHealth.ShareRadius > 0);

        Assert.IsTrue(abilities.TryGetValue(4340, out var healthPotion), "Health Potion");
        Assert.AreEqual(0f, healthPotion.ShareRadius);
    }

    [TestMethod]
    public void Load_RejectsANegativeShareRadius()
    {
        var path = Path.Combine(Path.GetTempPath(), $"item-abilities-{Guid.NewGuid():N}.json");
        File.WriteAllText(path,
            "[{\"AbilityId\":1,\"Health\":{\"Percent\":50},\"ShareRadius\":-1}," +
            "{\"AbilityId\":2,\"Health\":{\"Percent\":50},\"ShareRadius\":15}]");

        try
        {
            var abilities = new ItemAbilityDefinitionCollection(NullLogger.Instance);

            Assert.IsTrue(abilities.Load(path));
            CollectionAssert.AreEqual(new[] { 2 }, abilities.Keys.ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_RejectsAttacksThatDoNothing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"item-abilities-{Guid.NewGuid():N}.json");
        File.WriteAllText(path,
            "[{\"AbilityId\":1,\"Attack\":{\"HitEffectId\":9}}," +
            "{\"AbilityId\":2,\"Attack\":{\"Control\":\"Stun\"}}," +
            "{\"AbilityId\":3,\"Attack\":{\"Control\":\"Knockback\",\"ControlMs\":2000}}," +
            "{\"AbilityId\":4,\"Attack\":{\"Control\":\"Flee\",\"ControlMs\":2000,\"AroundCaster\":true}}," +
            "{\"AbilityId\":5,\"Attack\":{\"Damage\":300}}]");

        try
        {
            var abilities = new ItemAbilityDefinitionCollection(NullLogger.Instance);

            Assert.IsTrue(abilities.Load(path));
            CollectionAssert.AreEqual(new[] { 5 }, abilities.Keys.ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
