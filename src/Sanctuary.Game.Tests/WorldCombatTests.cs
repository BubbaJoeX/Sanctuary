using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions.Combat;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class WorldCombatTests
{
    private const ulong PlayerGuid = 1;
    private const ulong OtherPlayerGuid = 2;

    private static readonly Vector3 Home = Vector3.Zero;

    private static MobDefinition CreateMob() => new()
    {
        Key = "test",
        MaxHitpoints = 1000,
        Damage = 100,
        AttackIntervalMs = 2000,
        AttackRange = 3f,
        AggroRadius = 12f,
        LeashRadius = 40f
    };

    private static MobCandidate At(float x, ulong guid = PlayerGuid, bool canBeAttacked = true) =>
        new(guid, new Vector3(x, 0f, 0f), canBeAttacked);

    #region MobBrain

    [TestMethod]
    public void Idle_IgnoresPlayersOutsideAggroRadius()
    {
        var brain = new MobBrain(CreateMob());

        var action = brain.Think(0, Home, Home, [At(20f)]);

        Assert.AreEqual(MobActionType.None, action.Type);
        Assert.AreEqual(MobState.Idle, brain.State);
    }

    [TestMethod]
    public void Idle_AggroesOnPlayerInRangeAndChases()
    {
        var brain = new MobBrain(CreateMob());

        var action = brain.Think(0, Home, Home, [At(10f)]);

        Assert.AreEqual(MobActionType.Chase, action.Type);
        Assert.AreEqual(PlayerGuid, action.TargetGuid);
        Assert.IsTrue(action.Engaged);
        Assert.AreEqual(MobState.Fighting, brain.State);
    }

    [TestMethod]
    public void Engaged_OnlyOnTheFirstThinkOfAFight()
    {
        var brain = new MobBrain(CreateMob());

        brain.Think(0, Home, Home, [At(10f)]);
        var action = brain.Think(250, Home, Home, [At(10f)]);

        Assert.IsFalse(action.Engaged);
    }

    [TestMethod]
    public void Idle_IgnoresPlayersThatCannotBeAttacked()
    {
        var brain = new MobBrain(CreateMob());

        var action = brain.Think(0, Home, Home, [At(2f, canBeAttacked: false)]);

        Assert.AreEqual(MobActionType.None, action.Type);
    }

    [TestMethod]
    public void Idle_IgnoresPlayersFarAboveOrBelow()
    {
        var brain = new MobBrain(CreateMob());

        var action = brain.Think(0, Home, Home, [new MobCandidate(PlayerGuid, new Vector3(2f, 20f, 0f), true)]);

        Assert.AreEqual(MobActionType.None, action.Type);
    }

    [TestMethod]
    public void NoAggroRadius_OnlyFightsBackWhenHit()
    {
        var mob = CreateMob();
        mob.AggroRadius = 0f;
        var brain = new MobBrain(mob);

        Assert.AreEqual(MobActionType.None, brain.Think(0, Home, Home, [At(2f)]).Type);

        brain.AddThreat(PlayerGuid, 50);

        Assert.AreEqual(MobActionType.Attack, brain.Think(250, Home, Home, [At(2f)]).Type);
    }

    [TestMethod]
    public void InRange_AttacksOncePerInterval()
    {
        var brain = new MobBrain(CreateMob());

        Assert.AreEqual(MobActionType.Attack, brain.Think(0, Home, Home, [At(2f)]).Type);
        Assert.AreEqual(MobActionType.None, brain.Think(1000, Home, Home, [At(2f)]).Type);
        Assert.AreEqual(MobActionType.Attack, brain.Think(2000, Home, Home, [At(2f)]).Type);
    }

    [TestMethod]
    public void Target_IsWhoeverHurtItMost()
    {
        var brain = new MobBrain(CreateMob());

        brain.AddThreat(PlayerGuid, 100);
        brain.AddThreat(OtherPlayerGuid, 300);

        var action = brain.Think(0, Home, Home, [At(2f), At(2f, OtherPlayerGuid)]);

        Assert.AreEqual(OtherPlayerGuid, action.TargetGuid);
    }

    [TestMethod]
    public void TargetDies_SwitchesToTheNextAttacker()
    {
        var brain = new MobBrain(CreateMob());

        brain.AddThreat(PlayerGuid, 300);
        brain.AddThreat(OtherPlayerGuid, 100);
        brain.Think(0, Home, Home, [At(2f), At(2f, OtherPlayerGuid)]);

        var action = brain.Think(250, Home, Home, [At(2f, canBeAttacked: false), At(2f, OtherPlayerGuid)]);

        Assert.AreEqual(OtherPlayerGuid, action.TargetGuid);
        Assert.IsTrue(action.Engaged);
    }

    [TestMethod]
    public void LastTargetGone_WalksHomeThenResets()
    {
        var brain = new MobBrain(CreateMob());
        var position = new Vector3(15f, 0f, 0f);

        brain.Think(0, position, Home, [At(16f)]);

        var giveUp = brain.Think(250, position, Home, []);

        Assert.AreEqual(MobActionType.Return, giveUp.Type);
        Assert.AreEqual(Home, giveUp.Destination);
        Assert.AreEqual(MobState.Returning, brain.State);

        // Walking home it ignores the player it just left behind.
        Assert.AreEqual(MobActionType.Return, brain.Think(500, new Vector3(5f, 0f, 0f), Home, [At(6f)]).Type);

        Assert.AreEqual(MobActionType.Reset, brain.Think(750, Home, Home, [At(20f)]).Type);
        Assert.AreEqual(MobState.Idle, brain.State);
    }

    [TestMethod]
    public void PastLeash_GivesUp()
    {
        var brain = new MobBrain(CreateMob());

        brain.Think(0, Home, Home, [At(10f)]);

        var action = brain.Think(250, new Vector3(41f, 0f, 0f), Home, [At(43f)]);

        Assert.AreEqual(MobActionType.Return, action.Type);
        Assert.AreEqual(0, brain.Threat.Count);
    }

    [TestMethod]
    public void Returning_IgnoresDamage()
    {
        var brain = new MobBrain(CreateMob());

        brain.Think(0, Home, Home, [At(10f)]);
        brain.Think(250, new Vector3(41f, 0f, 0f), Home, [At(43f)]);

        brain.AddThreat(PlayerGuid, 500);

        Assert.AreEqual(MobState.Returning, brain.State);
    }

    [TestMethod]
    public void Dead_DoesNothingUntilRevived()
    {
        var brain = new MobBrain(CreateMob());

        brain.AddThreat(PlayerGuid, 100);
        brain.Kill();

        Assert.AreEqual(MobActionType.None, brain.Think(0, Home, Home, [At(2f)]).Type);

        brain.AddThreat(PlayerGuid, 100);
        Assert.AreEqual(MobState.Dead, brain.State);

        brain.Revive();

        Assert.AreEqual(MobActionType.Attack, brain.Think(250, Home, Home, [At(2f)]).Type);
    }

    #endregion

    #region CombatRules

    [TestMethod]
    public void RollDamage_StaysWithinVariance()
    {
        Assert.AreEqual(85, CombatRules.RollDamage(100, 0.15f, 0d));
        Assert.AreEqual(100, CombatRules.RollDamage(100, 0.15f, 0.5d));
        Assert.AreEqual(115, CombatRules.RollDamage(100, 0.15f, 1d));
    }

    [TestMethod]
    public void RollDamage_NeverZeroForAPositiveBase()
    {
        Assert.AreEqual(1, CombatRules.RollDamage(1, 1f, 0d));
        Assert.AreEqual(0, CombatRules.RollDamage(0, 0.15f, 0.5d));
    }

    [TestMethod]
    public void Mitigate_AppliesDamageReductionPercent()
    {
        // Darkthorne's Embrace: 25%.
        Assert.AreEqual(75, CombatRules.Mitigate(100, 25f));
        Assert.AreEqual(100, CombatRules.Mitigate(100, 0f));
        Assert.AreEqual(1, CombatRules.Mitigate(1, 90f));
        Assert.AreEqual(0, CombatRules.Mitigate(100, 100f));
    }

    private static readonly int[] CombatJobIds = [2, 11, 43];

    private static ClientPcProfile Profile(int id, int rank = 1) => new() { Id = id, Rank = rank };

    [TestMethod]
    public void PickCombatProfile_PrefersTheLastOneUsed()
    {
        List<ClientPcProfile> profiles = [Profile(14), Profile(2, rank: 10), Profile(43, rank: 2)];

        Assert.AreEqual(43, CombatRules.PickCombatProfileId(profiles, CombatJobIds, lastCombatProfileId: 43));
    }

    [TestMethod]
    public void PickCombatProfile_FallsBackToHighestRank()
    {
        List<ClientPcProfile> profiles = [Profile(14, rank: 20), Profile(2, rank: 3), Profile(43, rank: 7)];

        Assert.AreEqual(43, CombatRules.PickCombatProfileId(profiles, CombatJobIds, lastCombatProfileId: 0));
    }

    [TestMethod]
    public void PickCombatProfile_IgnoresALastJobNoLongerOwned()
    {
        List<ClientPcProfile> profiles = [Profile(2, rank: 3)];

        Assert.AreEqual(2, CombatRules.PickCombatProfileId(profiles, CombatJobIds, lastCombatProfileId: 11));
    }

    [TestMethod]
    public void PickCombatProfile_NoneWithoutACombatJob()
    {
        List<ClientPcProfile> profiles = [Profile(14), Profile(45)];

        Assert.AreEqual(0, CombatRules.PickCombatProfileId(profiles, CombatJobIds, lastCombatProfileId: 0));
    }

    #endregion

    #region Packets

    [TestMethod]
    public void UpdateHitpoints_IsGuidPercentMaxCurrent()
    {
        var data = new PlayerUpdatePacketUpdateHitpoints
        {
            Guid = 0x0102030405060708,
            HealthPercent = 50,
            MaxHitpoints = 1000,
            CurrentHitpoints = 500
        }.Serialize();

        // short OpCode, short SubOpCode, u64, int, int, int - the client rejects trailing bytes.
        Assert.AreEqual(2 + 2 + 8 + 4 + 4 + 4, data.Length);
        Assert.AreEqual((short)35, BitConverter.ToInt16(data, 0));
        Assert.AreEqual((short)5, BitConverter.ToInt16(data, 2));
        Assert.AreEqual(0x0102030405060708UL, BitConverter.ToUInt64(data, 4));
        Assert.AreEqual(50, BitConverter.ToInt32(data, 12));
        Assert.AreEqual(1000, BitConverter.ToInt32(data, 16));
        Assert.AreEqual(500, BitConverter.ToInt32(data, 20));
    }

    [TestMethod]
    public void HealthPercent_RoundsUpUntilDead()
    {
        Assert.AreEqual(100, PlayerUpdatePacketUpdateHitpoints.GetHealthPercent(1000, 1000));
        Assert.AreEqual(1, PlayerUpdatePacketUpdateHitpoints.GetHealthPercent(1, 1000));
        Assert.AreEqual(0, PlayerUpdatePacketUpdateHitpoints.GetHealthPercent(0, 1000));
    }

    [TestMethod]
    public void HitPointModification_Layout()
    {
        var data = new PlayerUpdatePacketHitPointModification
        {
            SourceGuid = 1,
            TargetGuid = 2,
            ShowDelta = true,
            MaxHitpoints = 1000,
            CurrentHitpoints = 750,
            Delta = -250,
            IsCrit = false
        }.Serialize();

        // u64, u64, bool, int, int, int, bool
        Assert.AreEqual(2 + 2 + 8 + 8 + 1 + 4 + 4 + 4 + 1, data.Length);
        Assert.AreEqual((short)35, BitConverter.ToInt16(data, 2));
        Assert.AreEqual(2UL, BitConverter.ToUInt64(data, 12));
        Assert.AreEqual(1, data[20]);
        Assert.AreEqual(1000, BitConverter.ToInt32(data, 21));
        Assert.AreEqual(750, BitConverter.ToInt32(data, 25));
        Assert.AreEqual(-250, BitConverter.ToInt32(data, 29));
    }

    #endregion

    #region Mobs.json

    [TestMethod]
    public void MobCollection_UnknownKeyFallsBackToDefault()
    {
        var mobs = new MobDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(mobs.Load([new MobDefinition { Key = "Default" }, new MobDefinition { Key = "Elite", MaxHitpoints = 8000 }], "test"));

        Assert.IsTrue(mobs.TryGetMob(" ELITE ", out var elite));
        Assert.AreEqual(8000, elite.MaxHitpoints);

        Assert.IsTrue(mobs.TryGetMob("missing", out var fallback));
        Assert.AreEqual(MobDefinition.DefaultKey, fallback.Key);

        Assert.IsTrue(mobs.TryGetMob(null, out _));
    }

    [TestMethod]
    public void MobCollection_RequiresADefaultEntry()
    {
        var mobs = new MobDefinitionCollection(NullLogger.Instance);

        Assert.IsFalse(mobs.Load([new MobDefinition { Key = "elite" }], "test"));
    }

    [TestMethod]
    public void MobCollection_RejectsALeashInsideTheAggroRadius()
    {
        var mobs = new MobDefinitionCollection(NullLogger.Instance);

        Assert.IsFalse(mobs.Load([new MobDefinition { Key = "default", AggroRadius = 20f, LeashRadius = 10f }], "test"));
    }

    [TestMethod]
    public void ShippedMobsJson_Loads()
    {
        var path = Path.Combine(FindSourceResources(), "Mobs.json");
        var entries = JsonSerializer.Deserialize<List<MobDefinition>>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.IsTrue(new MobDefinitionCollection(NullLogger.Instance).Load(entries, path));
    }

    [TestMethod]
    public void HostileNpc_ShowsRedNameplateAndIsClickableAsMob()
    {
        // A plain world NPC: named, with the referee cursor, but nothing for a player to click.
        var npc = new Npc(null!) { NameId = 1, CursorId = 5 };

        Assert.AreEqual(0, npc.GetAddNpcPacket().NameColor, "Neutral nameplates keep the client's own color.");
        Assert.IsFalse(npc.GetAddNpcPacket().IsInteractable);

        npc.Disposition = Npc.HostileDisposition;
        npc.Combat = new NpcCombat(npc, CreateMob(), null!);

        var packet = npc.GetAddNpcPacket();

        Assert.AreNotEqual(0, packet.NameColor, "The client's color table has no red to pick for hostile.");
        Assert.IsTrue(packet.IsInteractable, "Players click a mob to target and fight it.");
        Assert.IsFalse(packet.HideHealthBar);
    }

    private static string FindSourceResources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Resources", "Mobs.json")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "src/Resources/Mobs.json not found above the test output.");

        return Path.Combine(directory.FullName, "Resources");
    }

    #endregion
}
