using System;
using System.Collections.Generic;
using System.Linq;

using Sanctuary.Game.Resources;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.VehicleGames.Garage;

/// <summary>
/// The kart garage's rules: a player's loadouts (a kart and a derby car to start with), which parts go in which
/// slot, and the vehicle a loadout makes.
/// <para>
/// The client has nothing to build a vehicle from parts: the server sends it whole. A loadout's vehicle is the live
/// game's car for its chassis (Speedster and Racer karts, Wrecker and Crusher derby cars, as live drove them), in its
/// chassis' paint and tint, with its decal on.
/// </para>
/// </summary>
public static class VehicleLoadouts
{
    public const int KartRace = 1;
    public const int DemolitionDerby = 2;

    public const int ChassisSlot = 0;
    public const int DecalSlot = 8;

    // A loadout's items are kart race parts (Type 9) or derby parts (Type 10).
    private const int KartPartType = 9;
    private const int DerbyPartType = 10;

    /// <summary>
    /// The live player's kart (kt_kart08) and derby car (the Crusher in "claw" paint): what a loadout with nothing
    /// in its chassis slot drives.
    /// </summary>
    public const string StarterKart = "kt_kart08";
    public const string StarterDerbyCar = "dm_crusher_claw";

    /// <summary>
    /// A new character's two loadouts: the starter kart, and the derby car live gave the player (the Barbed
    /// Crusher Chassis, in "claw").
    /// </summary>
    public static List<(int Id, int Type, int[] Slots)> Defaults() =>
    [
        (1, KartRace, new int[VehicleLoadout.SlotCount]),
        (2, DemolitionDerby, Slots(chassis: 59601))
    ];

    private static int[] Slots(int chassis)
    {
        var slots = new int[VehicleLoadout.SlotCount];
        slots[ChassisSlot] = chassis;
        return slots;
    }

    public static int PartType(int vehicleType) => vehicleType == DemolitionDerby ? DerbyPartType : KartPartType;

    /// <summary>
    /// Whether an item goes in a loadout of <paramref name="vehicleType"/>, and in which slot: chassis have no slot
    /// of their own (category 139), every other part names its slot.
    /// </summary>
    public static bool TryGetSlot(ClientItemDefinitionCollection items, int itemId, int vehicleType, out int slot)
    {
        slot = -1;

        if (!items.TryGetValue(itemId, out var item) || item.Type != PartType(vehicleType))
            return false;

        slot = item.CategoryId == 139 ? ChassisSlot : item.Slot;

        return slot is >= 0 and < VehicleLoadout.SlotCount;
    }

    /// <summary>
    /// The vehicle a loadout makes: its chassis' car, painted, with its decal.
    /// </summary>
    public static VehicleConfig Compose(VehicleGameCollection games, ClientItemDefinitionCollection items, int vehicleType, IReadOnlyList<int> slots)
    {
        var name = vehicleType == DemolitionDerby ? StarterDerbyCar : StarterKart;
        uint? tint = null;
        string? body = null;
        string? decal = null;

        if (slots.Count > ChassisSlot && items.TryGetValue(slots[ChassisSlot], out var chassis) && chassis.Type == PartType(vehicleType))
        {
            name = Family(chassis.Type, chassis.Param1, chassis.Comment);

            if (chassis.Icon.TintId > 0)
                tint = (uint)chassis.Icon.TintId;

            if (!string.IsNullOrEmpty(chassis.TextureAlias))
                body = chassis.TextureAlias;
        }

        if (slots.Count > DecalSlot && items.TryGetValue(slots[DecalSlot], out var decalItem) && !string.IsNullOrEmpty(decalItem.TextureAlias))
            decal = decalItem.TextureAlias;

        if (!games.TryGetVehicle(name, out var config) && !games.TryGetVehicle(vehicleType == DemolitionDerby ? StarterDerbyCar : StarterKart, out config))
            throw new InvalidOperationException($"No vehicle {name}.");

        return tint is null && body is null && decal is null ? config : config.Repaint(tint, body, decal);
    }

    /// <summary>
    /// The live car a chassis is: its family by its Param1 (Speedster 1, Wrecker 2, Crusher 3; later chassis say it
    /// in their names).
    /// </summary>
    public static string Family(int type, int param1, string? comment)
    {
        var name = comment ?? string.Empty;

        if (type == DerbyPartType)
            return param1 == 2 || name.Contains("Wrecker", StringComparison.OrdinalIgnoreCase) || name.Contains("Ripper", StringComparison.OrdinalIgnoreCase)
                ? "dm_ripper_stripes"
                : StarterDerbyCar;

        return param1 == 1 || name.Contains("Speedster", StringComparison.OrdinalIgnoreCase) ? "kt_speedster_75m" : "kt_zipster";
    }

    public static VehicleLoadout ToPacket(int id, int type, IReadOnlyList<int> slots) => new()
    {
        Id = (uint)id,
        Unknown = (uint)type,
        Slots = Enumerable.Range(0, VehicleLoadout.SlotCount).Select(slot => slot < slots.Count ? (uint)Math.Max(0, slots[slot]) : 0u).ToList()
    };
}
