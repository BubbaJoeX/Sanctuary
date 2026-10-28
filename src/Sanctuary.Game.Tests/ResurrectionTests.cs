using System;
using System.IO;
using System.Linq;
using System.Numerics;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class ResurrectionTests
{
    private const ulong UserGuid = 1;
    private const ulong AllyGuid = 2;
    private const ulong OtherAllyGuid = 3;

    private const float Range = 10f;

    private const long RecoverMs = CombatRules.PlayerRespawnSeconds * 1000L;
    private const long ProtectionMs = CombatRules.ReviveProtectionSeconds * 1000L;

    private static readonly string Resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Resources"));

    private static ReviveCandidate Down(ulong guid, float x) => new(guid, new Vector3(x, 0f, 0f), IsKnockedOut: true);

    private static ReviveCandidate Standing(ulong guid, float x) => new(guid, new Vector3(x, 0f, 0f), IsKnockedOut: false);

    private static ReviveFailure Pick(ulong clickedGuid, ReviveCandidate[] players, out ulong targetGuid) =>
        CombatRules.PickReviveTarget(UserGuid, Vector3.Zero, clickedGuid, players, Range, out targetGuid);

    #region KnockOutTimer

    [TestMethod]
    public void KnockedOut_RecoversAtTheZoneStartOnceTheCountdownRunsOut()
    {
        var knockOut = new KnockOutTimer();

        knockOut.KnockOut(0);

        Assert.IsTrue(knockOut.IsDown);
        Assert.IsFalse(knockOut.TryRecover(RecoverMs - 1));
        Assert.IsTrue(knockOut.TryRecover(RecoverMs));
        Assert.IsFalse(knockOut.IsDown);
        Assert.IsFalse(knockOut.TryRecover(RecoverMs + 1000), "They only get up once.");
    }

    [TestMethod]
    public void Revive_CancelsTheTripToTheZoneStart()
    {
        var knockOut = new KnockOutTimer();

        knockOut.KnockOut(0);

        Assert.IsTrue(knockOut.TryRevive(1000));
        Assert.IsFalse(knockOut.IsDown);
        Assert.IsFalse(knockOut.TryRecover(RecoverMs));
        Assert.IsFalse(knockOut.TryRevive(2000), "Revived once, there's nothing left to revive.");
    }

    [TestMethod]
    public void Revive_OnlyWhileDown()
    {
        var knockOut = new KnockOutTimer();

        Assert.IsFalse(knockOut.TryRevive(0), "Never knocked out.");

        knockOut.KnockOut(0);
        Assert.IsTrue(knockOut.TryRecover(RecoverMs));

        Assert.IsFalse(knockOut.TryRevive(RecoverMs), "Already back up at the zone's start.");
        Assert.IsFalse(knockOut.IsProtected(RecoverMs));
    }

    [TestMethod]
    public void Revive_ProtectsForAMoment()
    {
        var knockOut = new KnockOutTimer();

        knockOut.KnockOut(0);
        knockOut.TryRevive(1000);

        Assert.IsTrue(knockOut.IsProtected(1000));
        Assert.IsTrue(knockOut.IsProtected(1000 + ProtectionMs - 1));
        Assert.IsFalse(knockOut.IsProtected(1000 + ProtectionMs));
    }

    [TestMethod]
    public void Protection_EndsOnAFightOrAnotherKnockOut()
    {
        var knockOut = new KnockOutTimer();

        knockOut.KnockOut(0);
        knockOut.TryRevive(1000);
        knockOut.EndProtection();

        Assert.IsFalse(knockOut.IsProtected(1000));

        knockOut.KnockOut(2000);
        knockOut.TryRevive(3000);
        knockOut.KnockOut(4000);

        Assert.IsFalse(knockOut.IsProtected(4000));
        Assert.IsTrue(knockOut.IsDown);
    }

    #endregion

    #region Player

    [TestMethod]
    public void PlayerRevive_LeavesAStandingPlayerAlone()
    {
        var player = new Player(null!, null!, null!, null!);
        var hitpoints = player.Hitpoints;

        Assert.IsFalse(player.IsKnockedOut);
        Assert.IsFalse(player.Revive(1));
        Assert.AreEqual(hitpoints, player.Hitpoints);
        Assert.IsFalse(player.IsReviveProtected);
    }

    #endregion

    #region CombatRules

    [TestMethod]
    public void ReviveHitpoints_IsAShareOfMaxHealth()
    {
        Assert.AreEqual(1250, CombatRules.ReviveHitpoints(2500, 50f));
        Assert.AreEqual(1251, CombatRules.ReviveHitpoints(2501, 50f), "Half a point rounds up.");
        Assert.AreEqual(2500, CombatRules.ReviveHitpoints(2500, 100f));
    }

    [TestMethod]
    public void ReviveHitpoints_AtLeastOneAndNoMoreThanMax()
    {
        Assert.AreEqual(1, CombatRules.ReviveHitpoints(2500, 0f));
        Assert.AreEqual(1, CombatRules.ReviveHitpoints(1, 10f));
        Assert.AreEqual(1, CombatRules.ReviveHitpoints(2500, float.NaN));
        Assert.AreEqual(1, CombatRules.ReviveHitpoints(0, 50f));
        Assert.AreEqual(2500, CombatRules.ReviveHitpoints(2500, 150f));
    }

    [TestMethod]
    public void PickReviveTarget_TheClickedFallenAlly()
    {
        var failure = Pick(AllyGuid, [Down(AllyGuid, 8f), Down(OtherAllyGuid, 2f)], out var target);

        Assert.AreEqual(ReviveFailure.None, failure);
        Assert.AreEqual(AllyGuid, target, "Not the nearer one.");
    }

    [TestMethod]
    public void PickReviveTarget_NearestFallenAllyWithoutAClick()
    {
        var failure = Pick(0, [Down(AllyGuid, 8f), Standing(4, 1f), Down(OtherAllyGuid, 3f)], out var target);

        Assert.AreEqual(ReviveFailure.None, failure);
        Assert.AreEqual(OtherAllyGuid, target);
    }

    [TestMethod]
    public void PickReviveTarget_AClickedMobOrUnseenPlayerFallsBackToTheNearest()
    {
        Assert.AreEqual(ReviveFailure.None, Pick(999, [Down(AllyGuid, 5f)], out var target));
        Assert.AreEqual(AllyGuid, target);
    }

    [TestMethod]
    public void PickReviveTarget_AClickedStandingPlayerIsNotSwappedForAnother()
    {
        var failure = Pick(AllyGuid, [Standing(AllyGuid, 2f), Down(OtherAllyGuid, 3f)], out var target);

        Assert.AreEqual(ReviveFailure.NotKnockedOut, failure);
        Assert.AreEqual(0UL, target);
    }

    [TestMethod]
    public void PickReviveTarget_AClickedFallenAllyOutOfRange()
    {
        var failure = Pick(AllyGuid, [Down(AllyGuid, Range + 0.5f), Down(OtherAllyGuid, 3f)], out var target);

        Assert.AreEqual(ReviveFailure.TooFar, failure);
        Assert.AreEqual(0UL, target);

        Assert.AreEqual(ReviveFailure.None, Pick(AllyGuid, [Down(AllyGuid, Range)], out _), "Range is inclusive.");
    }

    [TestMethod]
    public void PickReviveTarget_NobodyDownInRange()
    {
        Assert.AreEqual(ReviveFailure.NoFallenAlly, Pick(0, [], out _));
        Assert.AreEqual(ReviveFailure.NoFallenAlly, Pick(0, [Standing(AllyGuid, 1f), Down(OtherAllyGuid, 20f)], out var target));
        Assert.AreEqual(0UL, target);
    }

    [TestMethod]
    public void PickReviveTarget_NeverTheUser()
    {
        // Clicking yourself counts as no click; you can't revive yourself.
        Assert.AreEqual(ReviveFailure.NoFallenAlly, Pick(UserGuid, [Down(UserGuid, 0f)], out _));

        Assert.AreEqual(ReviveFailure.None, Pick(UserGuid, [Down(UserGuid, 0f), Down(AllyGuid, 4f)], out var target));
        Assert.AreEqual(AllyGuid, target);
    }

    #endregion

    #region ItemAbilities.json

    [TestMethod]
    public void ItemAbilitiesJson_ResurrectionScroll()
    {
        var abilities = new ItemAbilityDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(abilities.Load(Path.Combine(Resources, "ItemAbilities.json")));

        // Item 11018's ActivatableAbilityId.
        Assert.IsTrue(abilities.TryGetValue(1654, out var scroll), "Resurrection Scroll");

        var revive = scroll.Revive;

        Assert.IsNotNull(revive);
        Assert.AreEqual(50f, revive.HealthPercent);
        Assert.AreEqual(Range, revive.Range);
        Assert.AreEqual(4002, revive.EffectId, "PFX_magic-resurrect");
        Assert.IsFalse(scroll.AffectsUser, "It does nothing to whoever reads it.");
        Assert.IsNull(scroll.Attack);
    }

    [TestMethod]
    public void Load_RejectsReviveWithoutHealthOrRange()
    {
        var path = Path.Combine(Path.GetTempPath(), $"item-abilities-{Guid.NewGuid():N}.json");
        File.WriteAllText(path,
            "[{\"AbilityId\":1,\"Revive\":{\"HealthPercent\":0}}," +
            "{\"AbilityId\":2,\"Revive\":{\"HealthPercent\":150}}," +
            "{\"AbilityId\":3,\"Revive\":{\"HealthPercent\":50,\"Range\":0}}," +
            "{\"AbilityId\":4,\"Revive\":{\"HealthPercent\":50,\"EffectId\":-1}}," +
            "{\"AbilityId\":5,\"Revive\":{\"HealthPercent\":50}}]");

        try
        {
            var abilities = new ItemAbilityDefinitionCollection(NullLogger.Instance);

            Assert.IsTrue(abilities.Load(path));
            CollectionAssert.AreEqual(new[] { 5 }, abilities.Keys.ToArray());
            Assert.AreEqual(10f, abilities[5].Revive!.Range, "Range defaults to 10 m.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    #endregion
}
