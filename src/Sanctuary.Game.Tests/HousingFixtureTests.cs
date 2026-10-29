using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Core.IO;
using Sanctuary.Game.Housing;
using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class HousingFixtureTests
{
    private static string GetResourcesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Resources", "HousingFixtures.json")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "Could not find the Resources directory.");

        return Path.Combine(directory.FullName, "Resources");
    }

    [TestMethod]
    public void HousingFixtures_AreFurnitureWithModels()
    {
        var resourcesDirectory = GetResourcesDirectory();

        var fixtures = new HousingFixtureDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(fixtures.Load(Path.Combine(resourcesDirectory, "HousingFixtures.json")));

        using var stream = File.OpenRead(Path.Combine(resourcesDirectory, "ClientItemDefinitions.json"));
        using var document = JsonDocument.Parse(stream);

        var items = document.RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("Id").GetInt32());

        var models = File.ReadLines(Path.Combine(resourcesDirectory, "Models.txt"))
            .Where(line => line.Length > 0 && char.IsDigit(line[0]))
            .Select(line => line.Split('^')[1])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // HousingManager.IsFurniture: a category in the furniture group (50), or the housing item type (29).
        var furnitureCategories = File.ReadLines(Path.Combine(resourcesDirectory, "ItemCategoryGroups.txt"))
            .Select(line => line.Split('^'))
            .Where(parts => parts[0] == "50")
            .Select(parts => int.Parse(parts[1]))
            .ToHashSet();

        foreach (var itemDefinitionId in fixtures.Keys)
        {
            Assert.IsTrue(items.TryGetValue(itemDefinitionId, out var item), $"Item {itemDefinitionId} doesn't exist.");

            var isFurniture = (item.TryGetProperty("Type", out var type) && type.GetInt32() == 29)
                || (item.TryGetProperty("CategoryId", out var category) && furnitureCategories.Contains(category.GetInt32()));

            Assert.IsTrue(isFurniture, $"Item {itemDefinitionId} isn't furniture.");
            Assert.IsTrue(item.TryGetProperty("ModelName", out var modelName) && models.Contains(modelName.GetString()!),
                $"Item {itemDefinitionId} has no placeable model.");

            if (fixtures[itemDefinitionId].ModelName is { } housingModel)
                Assert.IsTrue(models.Contains(housingModel), $"Item {itemDefinitionId}'s housing model {housingModel} isn't in Models.txt.");
        }
    }

    [TestMethod]
    public void HousingFixtures_CoverEveryKind()
    {
        var fixtures = new HousingFixtureDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(fixtures.Load(Path.Combine(GetResourcesDirectory(), "HousingFixtures.json")));

        // Small Launch Pad, Short Range Elevator Platform, High-rise Escalator Platform, Two-Way Teleporter.
        Assert.AreEqual(4224, fixtures[2929].AreaDefinitionId);
        Assert.AreEqual(2, fixtures[10341].Platform!.Path.Count);
        Assert.AreEqual(3, fixtures[10345].Platform!.Path.Count);
        Assert.AreEqual(HousingTeleporterMode.TwoWay, fixtures[10358].Teleporter!.Mode);
        Assert.AreEqual(HousingTeleporterMode.OneWay, fixtures[10362].Teleporter!.Mode);
        Assert.AreEqual(HousingTeleporterMode.MultiWay, fixtures[10363].Teleporter!.Mode);

        // Piano C1 and Fart C1 music blocks are placed as the solid housing blocks, not their store previews.
        Assert.AreEqual("hsg_piano_block_01.adr", fixtures[38901].ModelName);
        Assert.AreEqual("hsg_fart_block_01.adr", fixtures[38917].ModelName);
    }

    [TestMethod]
    public void HousingFixture_Validation_RejectsBrokenEntries()
    {
        Assert.IsFalse(HousingFixtureDefinitionCollection.IsValid(new HousingFixtureDefinition { AreaDefinitionId = 1 }, out _));
        Assert.IsFalse(HousingFixtureDefinitionCollection.IsValid(new HousingFixtureDefinition { ItemDefinitionIds = [1] }, out _));

        var onePoint = new HousingFixtureDefinition
        {
            ItemDefinitionIds = [1],
            Platform = new HousingPlatformDefinition { Path = [[0, 0, 0]], Speed = 1f }
        };
        Assert.IsFalse(HousingFixtureDefinitionCollection.IsValid(onePoint, out _));

        var still = new HousingFixtureDefinition
        {
            ItemDefinitionIds = [1],
            Platform = new HousingPlatformDefinition { Path = [[0, 0, 0], [0, 1, 0]], Speed = 0f }
        };
        Assert.IsFalse(HousingFixtureDefinitionCollection.IsValid(still, out _));

        var both = new HousingFixtureDefinition
        {
            ItemDefinitionIds = [1],
            Platform = new HousingPlatformDefinition { Path = [[0, 0, 0], [0, 1, 0]], Speed = 1f },
            Teleporter = new HousingTeleporterDefinition()
        };
        Assert.IsFalse(HousingFixtureDefinitionCollection.IsValid(both, out _));
    }

    [TestMethod]
    public void PlatformRoute_WaitsAtCornersAndTravelsBetweenThem()
    {
        // 10 m at 2 m/s: 5 s each way, with a 2 s wait at each end.
        var route = new PlatformRoute([Vector3.Zero, new Vector3(0, 10, 0)], 2f, 2f);

        Assert.AreEqual(14f, route.CycleSeconds, 1e-4f);

        AssertSample(route, 1f, Vector3.Zero, false);
        AssertSample(route, 4.5f, new Vector3(0, 5, 0), true);
        AssertSample(route, 8f, new Vector3(0, 10, 0), false);
        AssertSample(route, 11.5f, new Vector3(0, 5, 0), true);
        AssertSample(route, 14f + 4.5f, new Vector3(0, 5, 0), true);
    }

    [TestMethod]
    public void PlatformRoute_LoopsBackToTheFirstCorner()
    {
        var route = new PlatformRoute([Vector3.Zero, new Vector3(0, 0, 20), new Vector3(0, 20, 20)], 2f, 0f);

        // 20 m, 20 m and the 28.3 m slope home.
        Assert.AreEqual(20f + 10f * MathF.Sqrt(2f), route.CycleSeconds, 1e-3f);

        AssertSample(route, 15f, new Vector3(0, 10, 20), true);
        AssertSample(route, 20f + MathF.Sqrt(2f) * 5f, new Vector3(0, 10, 10), true);
    }

    [TestMethod]
    public void PlatformRoute_FromLocal_TurnsWithThePlatform()
    {
        // Facing +X (a quarter turn): forward 10 m ends up 10 m along X.
        var route = PlatformRoute.FromLocal([[0, 0, 0], [0, 0, 10]], new Vector3(100, 5, 50), MathF.PI / 2f, 1f, 0f);

        AssertSample(route, 0f, new Vector3(100, 5, 50), true);
        AssertSample(route, 10f, new Vector3(110, 5, 50), true);
    }

    private static void AssertSample(PlatformRoute route, float seconds, Vector3 expected, bool moving)
    {
        var (position, isMoving) = route.Sample(seconds);

        Assert.AreEqual(expected.X, position.X, 1e-3f, $"x at {seconds}s");
        Assert.AreEqual(expected.Y, position.Y, 1e-3f, $"y at {seconds}s");
        Assert.AreEqual(expected.Z, position.Z, 1e-3f, $"z at {seconds}s");
        Assert.AreEqual(moving, isMoving, $"moving at {seconds}s");
    }

    [TestMethod]
    public void TwoWayTeleporters_PairUpInPlacementOrder()
    {
        List<TeleporterLinks.Pad> pads =
        [
            new(1, HousingTeleporterMode.TwoWay),
            new(2, HousingTeleporterMode.OneWay),
            new(3, HousingTeleporterMode.TwoWay),
            new(4, HousingTeleporterMode.TwoWay)
        ];

        var random = new Random(1);

        Assert.AreEqual(3ul, TeleporterLinks.GetDestination(pads, 1, random));
        Assert.AreEqual(1ul, TeleporterLinks.GetDestination(pads, 3, random));

        // The third two-way pad has no partner yet, and the lone one-way pad goes nowhere.
        Assert.IsNull(TeleporterLinks.GetDestination(pads, 4, random));
        Assert.IsNull(TeleporterLinks.GetDestination(pads, 2, random));

        CollectionAssert.AreEqual(new List<(ulong, ulong)> { (1, 3) }, TeleporterLinks.GetLinks(pads));
    }

    [TestMethod]
    public void OneWayTeleporters_OnlySendFromTheFirstOfEachPair()
    {
        List<TeleporterLinks.Pad> pads =
        [
            new(10, HousingTeleporterMode.OneWay),
            new(11, HousingTeleporterMode.OneWay)
        ];

        var random = new Random(1);

        Assert.AreEqual(11ul, TeleporterLinks.GetDestination(pads, 10, random));
        Assert.IsNull(TeleporterLinks.GetDestination(pads, 11, random));
        Assert.IsTrue(TeleporterLinks.IsLinked(pads, 11));
    }

    [TestMethod]
    public void MultiWayTeleporters_NeverSendToThemselves()
    {
        List<TeleporterLinks.Pad> pads =
        [
            new(1, HousingTeleporterMode.MultiWay),
            new(2, HousingTeleporterMode.MultiWay),
            new(3, HousingTeleporterMode.MultiWay),
            new(4, HousingTeleporterMode.TwoWay)
        ];

        var random = new Random(7);
        var seen = new HashSet<ulong>();

        for (var i = 0; i < 200; i++)
        {
            var destination = TeleporterLinks.GetDestination(pads, 2, random);

            Assert.IsNotNull(destination);
            Assert.AreNotEqual(2ul, destination.Value);
            Assert.AreNotEqual(4ul, destination.Value);

            seen.Add(destination.Value);
        }

        CollectionAssert.AreEquivalent(new[] { 1ul, 3ul }, seen.ToArray());
        Assert.AreEqual(3, TeleporterLinks.GetLinks(pads).Count);
    }

    [TestMethod]
    public void TeleporterPad_ChargesThenSends()
    {
        var pad = new TeleporterPad();

        var (started, ready) = pad.Update([7ul], 1000);
        CollectionAssert.AreEqual(new[] { 7ul }, started);
        Assert.AreEqual(0, ready.Count);

        (started, ready) = pad.Update([7ul], 1000 + TeleporterPad.ChargeMilliseconds - 1);
        Assert.AreEqual(0, started.Count + ready.Count);

        (_, ready) = pad.Update([7ul], 1000 + TeleporterPad.ChargeMilliseconds);
        CollectionAssert.AreEqual(new[] { 7ul }, ready);
    }

    [TestMethod]
    public void TeleporterPad_SteppingOffCancels()
    {
        var pad = new TeleporterPad();

        pad.Update([7ul], 0);
        pad.Update([], 500);

        Assert.IsFalse(pad.IsCharging(7));

        var (started, _) = pad.Update([7ul], 600);
        CollectionAssert.AreEqual(new[] { 7ul }, started);
    }

    [TestMethod]
    public void TeleporterPad_ArrivalsStepOffBeforeGoingAgain()
    {
        var pad = new TeleporterPad();

        pad.MarkArrived(7);

        var (started, ready) = pad.Update([7ul], 0);
        Assert.AreEqual(0, started.Count + ready.Count);

        (started, ready) = pad.Update([7ul], 10_000);
        Assert.AreEqual(0, started.Count + ready.Count);

        pad.Update([], 10_100);

        (started, _) = pad.Update([7ul], 10_200);
        CollectionAssert.AreEqual(new[] { 7ul }, started);
    }

    private static byte[] GetPositionOnPlatformPacket(bool withRelativePosition)
    {
        using var writer = new PacketWriter();

        writer.Write(PlayerUpdatePacketUpdatePositionOnPlatform.OpCode);
        writer.Write(42ul);
        writer.Write(new Vector4(10, 20, 30, 1), true);
        writer.Write(new Quaternion(0, 0, 1, 0), true);
        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write(100_000_000_005ul);

        if (withRelativePosition)
            writer.Write(new Vector4(0.5f, 0.8f, -1f, 1), true);

        return writer.Buffer;
    }

    [TestMethod]
    public void PositionOnPlatform_Deserializes()
    {
        Assert.IsTrue(PlayerUpdatePacketUpdatePositionOnPlatform.TryDeserialize(GetPositionOnPlatformPacket(true), out var packet));

        Assert.AreEqual(new Vector4(10, 20, 30, 1), packet.Update.Position);
        Assert.AreEqual(100_000_000_005ul, packet.PlatformGuid);
        Assert.AreEqual(new Vector4(0.5f, 0.8f, -1f, 1), packet.RelativePosition);

        Assert.IsTrue(PlayerUpdatePacketUpdatePositionOnPlatform.TryDeserialize(GetPositionOnPlatformPacket(false), out packet));
        Assert.IsNull(packet.RelativePosition);
    }
}
