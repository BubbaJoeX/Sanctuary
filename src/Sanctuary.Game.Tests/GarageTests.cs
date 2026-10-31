using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Core.IO;
using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.VehicleGames.Garage;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public class GarageTests
{
    [TestMethod]
    public void Defaults_AreTheLivePlayersVehicles()
    {
        var (games, items) = Load();

        var defaults = VehicleLoadouts.Defaults();

        Assert.AreEqual(2, defaults.Count);
        Assert.IsTrue(defaults.All(loadout => loadout.Slots.Length == VehicleLoadout.SlotCount), "The client writes slots by index: all 16.");

        var (_, kartType, kartSlots) = defaults.Single(loadout => loadout.Type == VehicleLoadouts.KartRace);
        var (_, derbyType, derbySlots) = defaults.Single(loadout => loadout.Type == VehicleLoadouts.DemolitionDerby);

        // The live race's player kart, as it was; the live derby's player car (a Crusher in "claw").
        Assert.IsTrue(games.TryGetVehicle(VehicleLoadouts.StarterKart, out var kart));
        CollectionAssert.AreEqual(kart.Data, VehicleLoadouts.Compose(games, items, kartType, kartSlots).Data);

        var derby = VehicleLoadouts.Compose(games, items, derbyType, derbySlots);

        Assert.AreEqual("sg_dm_crusher_body_bbec_002.cdt", derby.CollisionModel);
        Assert.AreEqual("claw", derby.BodyTextureAlias);
    }

    [TestMethod]
    public void Compose_PaintsTheChassisFamilyAndPutsTheDecalOn()
    {
        var (games, items) = Load();

        var slots = new int[VehicleLoadout.SlotCount];

        // Metal Petal Speedster Chassis (flower) with the Taka Racing Wheeler Decal (takadoll).
        slots[VehicleLoadouts.ChassisSlot] = 58578;
        slots[VehicleLoadouts.DecalSlot] = 2199;

        var kart = VehicleLoadouts.Compose(games, items, VehicleLoadouts.KartRace, slots);

        Assert.AreEqual("sg_kt_speedster_body_bbec_002.cdt", kart.CollisionModel);
        Assert.AreEqual("flower", kart.BodyTextureAlias);
        Assert.AreEqual("takadoll", kart.DecalTextureAlias);
        Assert.AreEqual((uint)items[58578].Icon.TintId, kart.BodyTintId);

        // It still reads whole, as the client reads it.
        var reader = new PacketReader(kart.Data);
        Assert.IsTrue(VehicleConfig.TryRead(ref reader, out _));
        Assert.AreEqual(0, reader.RemainingLength);

        // A Racer is the other kart; a Wrecker the Ripper.
        slots[VehicleLoadouts.ChassisSlot] = 58566;
        Assert.AreEqual("sg_kt_zipster_body_bbec_002.cdt", VehicleLoadouts.Compose(games, items, VehicleLoadouts.KartRace, slots).CollisionModel);

        var derbySlots = new int[VehicleLoadout.SlotCount];
        derbySlots[VehicleLoadouts.ChassisSlot] = 58588;

        var derby = VehicleLoadouts.Compose(games, items, VehicleLoadouts.DemolitionDerby, derbySlots);

        Assert.AreEqual("sg_dm_ripper_body_bbec_002.cdt", derby.CollisionModel);
        Assert.AreEqual("flower", derby.BodyTextureAlias);

        // A kart chassis does nothing for a derby car.
        Assert.AreEqual("sg_dm_crusher_body_bbec_002.cdt", VehicleLoadouts.Compose(games, items, VehicleLoadouts.DemolitionDerby, slots).CollisionModel);
    }

    [TestMethod]
    public void Parts_GoInTheirOwnSlotsOfTheirOwnVehicles()
    {
        var (_, items) = Load();

        Assert.IsTrue(VehicleLoadouts.TryGetSlot(items, 59606, VehicleLoadouts.KartRace, out var engine));
        Assert.AreEqual(1, engine, "Racing Stock Engine");

        Assert.IsTrue(VehicleLoadouts.TryGetSlot(items, 2199, VehicleLoadouts.KartRace, out var decal));
        Assert.AreEqual(VehicleLoadouts.DecalSlot, decal);

        Assert.IsTrue(VehicleLoadouts.TryGetSlot(items, 58586, VehicleLoadouts.DemolitionDerby, out var chassis));
        Assert.AreEqual(VehicleLoadouts.ChassisSlot, chassis, "Ripper Wrecker Chassis");

        Assert.IsFalse(VehicleLoadouts.TryGetSlot(items, 58586, VehicleLoadouts.KartRace, out _), "A derby chassis on a kart.");
        Assert.IsFalse(VehicleLoadouts.TryGetSlot(items, 48308, VehicleLoadouts.KartRace, out _), "A whole vehicle isn't a part.");
    }

    [TestMethod]
    public void Loadouts_WriteAsTheClientReadsThem()
    {
        var vehicles = new VehicleStruct
        {
            NextLoadoutId = 3,
            Loadouts = VehicleLoadouts.Defaults().Select(loadout => VehicleLoadouts.ToPacket(loadout.Id, loadout.Type, loadout.Slots)).ToList()
        };

        using var writer = new PacketWriter();

        vehicles.Serialize(writer);

        var reader = new PacketReader(writer.Buffer);

        // u32 NextLoadoutId, then the map: u32 count, (u32 key, the loadout) each.
        Assert.IsTrue(reader.TryRead(out uint next) && next == 3);
        Assert.IsTrue(reader.TryRead(out uint count) && count == 2);

        for (var i = 0; i < count; i++)
        {
            Assert.IsTrue(reader.TryRead(out uint key) && reader.TryRead(out uint id) && key == id);
            Assert.IsTrue(reader.TryRead(out uint _));
            Assert.IsTrue(reader.TryRead(out uint slots) && slots == VehicleLoadout.SlotCount);
            reader.Advance(4 * VehicleLoadout.SlotCount + 5 * 4 + 1);
        }

        Assert.AreEqual(0, reader.RemainingLength);
    }

    private static (VehicleGameCollection Games, ClientItemDefinitionCollection Items) Load()
    {
        var resources = DerbyTests.FindSourceResources();
        var items = new ClientItemDefinitionCollection(NullLogger.Instance);

        Assert.IsTrue(items.Load(Path.Combine(resources, "ClientItemDefinitions.json")));

        return (DerbyTests.LoadGames(), items);
    }
}
