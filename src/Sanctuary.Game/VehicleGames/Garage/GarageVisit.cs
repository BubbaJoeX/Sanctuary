using System;
using System.Buffers.Binary;
using System.Linq;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.VehicleGames.Garage;

/// <summary>
/// A visit to the Kart &amp; Demo Derby Garage (sg_racing_garage): an encounter of kind 12, which makes the client
/// open the garage as the zone loads and ask for its preview vehicle. The player picks a kart or derby car, puts
/// parts from their inventory on it or takes them off, and the server keeps their loadouts and shows the vehicle as
/// it now is. Leaving is the encounter's exit.
/// </summary>
public sealed class GarageVisit : ArenaMatch
{
    // The garage's vehicle list shows one type at a time; it opens on the karts.
    private int _type = VehicleLoadouts.KartRace;

    public GarageVisit(int id, VehicleGameStageDefinition stage, WorldZone zone, VehicleGameManager manager, ILogger logger)
        : base(id, stage, zone, manager, logger)
    {
    }

    /// <summary>
    /// The garage needs no game switched on: the encounter's kind opens it.
    /// </summary>
    public override int CharacterStateBit => 0;

    public override void OnPacket(ArenaParticipant participant, short opCode, ReadOnlySpan<byte> data)
    {
        if (opCode != BaseVehiclePartPacket.OpCode || data.Length < 4)
            return;

        var player = participant.Player;

        switch (BinaryPrimitives.ReadUInt16LittleEndian(data[2..]))
        {
            case VehiclePartPacketRequestVehicleConfig.OpCode when VehiclePartPacketRequestVehicleConfig.TryDeserialize(data, out _):
                SendPreview(player);
                break;

            case VehiclePartPacketRequestSelectVehicle.OpCode when VehiclePartPacketRequestSelectVehicle.TryDeserialize(data, out var select):
                if (Manager.SelectVehicle(player, (int)select.VehicleType, (int)select.VehicleId))
                {
                    _type = (int)select.VehicleType;
                    SendPreview(player);
                }
                break;

            case VehiclePartPacketRequestVehicleEquipmentArrayUpdate.OpCode when VehiclePartPacketRequestVehicleEquipmentArrayUpdate.TryDeserialize(data, out var equip):
                Equip(player, (int)equip.ItemId);
                break;

            case VehiclePartPacketRequestRemovePartFromSlot.OpCode when VehiclePartPacketRequestRemovePartFromSlot.TryDeserialize(data, out var remove):
                SetSlot(player, (int)remove.Slot, 0);
                break;
        }
    }

    public override void Update(long now, float deltaSeconds)
    {
    }

    /// <summary>
    /// A part from the player's inventory onto the vehicle on show: it must be theirs, and a part for its type.
    /// </summary>
    private void Equip(Player player, int itemId)
    {
        // The client names the item by its inventory id or its definition.
        var item = player.Items.FirstOrDefault(item => item.Id == itemId) ?? player.Items.FirstOrDefault(item => item.Definition == itemId);

        if (item is null)
        {
            Logger.LogWarning("{player} tried to put item {item}, which they don't have, on their vehicle.", player.Name.FullName, itemId);
            return;
        }

        if (!VehicleLoadouts.TryGetSlot(Manager.ResourceManager.ClientItemDefinitions, item.Definition, _type, out var slot))
            return;

        SetSlot(player, slot, item.Definition);
    }

    private void SetSlot(Player player, int slot, int itemDefinitionId)
    {
        if (slot is < 0 or >= VehicleLoadout.SlotCount || VehicleLoadoutStore.Active(player, _type) is not { } loadout)
            return;

        loadout.Slots[slot] = (uint)itemDefinitionId;

        try
        {
            VehicleLoadoutStore.Save(player, loadout, Manager.DbContextFactory);
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogError(ex, "Garage {encounter:X}: the loadout didn't save.", Id);
            return;
        }

        player.SendTunneled(new VehicleUpdateItemPacket { LoadoutId = loadout.Id, Slot = (uint)slot, ItemId = (uint)itemDefinitionId });

        SendPreview(player);

        if (slot == VehicleLoadouts.DecalSlot)
            player.SendTunneled(new VehiclePartPacketVehicleUpdateDecal { Decal = Manager.ResourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var decal) ? decal.TextureAlias ?? string.Empty : string.Empty });
    }

    /// <summary>
    /// The vehicle on show: the active loadout of the type the list shows, as it's kitted out.
    /// </summary>
    private void SendPreview(Player player)
    {
        var loadout = VehicleLoadoutStore.Active(player, _type);

        player.SendTunneled(new VehiclePartPacketVehicleConfig
        {
            Config = Manager.PlayerVehicle(player, _type),
            SelectedVehicleId = loadout?.Id ?? 0,
            VehicleTypeFilter = (uint)_type
        });
    }
}
