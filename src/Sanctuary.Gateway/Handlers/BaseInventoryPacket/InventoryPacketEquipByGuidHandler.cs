using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class InventoryPacketEquipByGuidHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(InventoryPacketEquipByGuidHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!InventoryPacketEquipByGuid.TryDeserialize(data, out var inventoryPacketEquipByGuid))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(InventoryPacketEquipByGuid));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(InventoryPacketEquipByGuid), inventoryPacketEquipByGuid);

        var clientItem = connection.Player.Items.SingleOrDefault(x => x.Id == inventoryPacketEquipByGuid.Guid);

        if (clientItem is null)
        {
            _logger.LogWarning("User tried to equip unknown item. {guid}", inventoryPacketEquipByGuid.Guid);
            return true;
        }

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var clientItemDefinition))
        {
            _logger.LogWarning("User tried to equip unknown item definition. {guid} {definition}", inventoryPacketEquipByGuid.Guid, clientItem.Definition);
            return true;
        }

        if (clientItemDefinition.Type != 1)
        {
            _logger.LogWarning("User tried to equip non-equipment item. {guid} {definition} {type}",
                inventoryPacketEquipByGuid.Guid, clientItem.Definition, clientItemDefinition.Type);
            return true;
        }

        if (!_resourceManager.ItemClasses.TryGetValue(clientItemDefinition.Class, out var itemClass))
        {
            _logger.LogWarning("User tried to equip item with unknown class. {guid} {definition} {class}",
                inventoryPacketEquipByGuid.Guid, clientItem.Definition, clientItemDefinition.Class);
            return true;
        }

        var profile = connection.Player.Profiles.SingleOrDefault(x => x.Id == inventoryPacketEquipByGuid.ProfileId);

        if (profile is null)
        {
            _logger.LogWarning("Invalid player profile. {guid} {profile}", inventoryPacketEquipByGuid.Guid, inventoryPacketEquipByGuid.ProfileId);
            return true;
        }

        if (!EquipmentHelper.CheckRank(connection.Player, profile, clientItemDefinition))
            return true;

        var oldItemId = EquipmentHelper.SetProfileItem(profile, clientItem, clientItemDefinition);

        var playerGuid = connection.Player.Guid;
        var profileId = inventoryPacketEquipByGuid.ProfileId;
        var itemId = clientItem.Id;

        connection.QueueWork(() =>
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            if (!EquipmentHelper.SaveProfileItem(dbContext, playerGuid, profileId, oldItemId, itemId))
                _logger.LogWarning("Failed to save the equipped item to the database.");
        });

        EquipmentHelper.SendEquipped(connection.Player, profileId, inventoryPacketEquipByGuid.Slot, clientItem, clientItemDefinition);

        // A new rod casts further or less far.
        (connection.Player.Zone as WorldZone)?.Fishing?.OnGearChanged(connection.Player);

        return true;
    }
}
