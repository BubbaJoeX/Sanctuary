using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

/// <summary>
/// Opening a gift from the inventory (gifts go out as the client's type 28): Use asks first, and OK on the confirm
/// opens one.
/// </summary>
[PacketHandler]
public static class InventoryPacketImmediateActivationHandler
{
    /// <summary>
    /// "Open Present": the confirm's text, under the gift's name.
    /// </summary>
    public const int OpenPresentStringId = 441928;

    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IRewardManager _rewardManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(InventoryPacketImmediateActivationHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _rewardManager = serviceProvider.GetRequiredService<IRewardManager>();
    }

    public static bool HandleCheck(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!InventoryPacketCheckImmediateActivationItem.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(InventoryPacketCheckImmediateActivationItem));
            return false;
        }

        var player = connection.Player;

        if (!TryGetGift(connection, packet.ItemGuid, out var definitionId, out var nameId))
            return true;

        player.SendTunneled(new ClientUpdatePacketImmediateActivationCheck
        {
            ItemGuid = packet.ItemGuid,
            TitleStringId = nameId,
            BodyStringId = OpenPresentStringId
        });

        return true;
    }

    public static bool HandleUse(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!InventoryPacketUseImmediateActivationItem.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(InventoryPacketUseImmediateActivationItem));
            return false;
        }

        var player = connection.Player;

        if (!TryGetGift(connection, packet.ItemGuid, out var definitionId, out _))
            return true;

        // One at a time, taken before it opens so it can't open twice.
        if (_rewardManager.TryTakeItem(player, definitionId, 1))
            _rewardManager.TryOpenGift(player, definitionId);

        return true;
    }

    private static bool TryGetGift(GatewayConnection connection, int itemGuid, out int definitionId, out int nameId)
    {
        definitionId = 0;
        nameId = 0;

        var item = connection.Player.Items.FirstOrDefault(owned => owned.Id == itemGuid);

        if (item is null || !_resourceManager.Gifts.ContainsKey(item.Definition) ||
            !_resourceManager.ClientItemDefinitions.TryGetValue(item.Definition, out var definition))
        {
            ChatHelper.SendSystemMessage(connection.Player, "That can't be opened.");
            return false;
        }

        definitionId = item.Definition;
        nameId = definition.NameId;
        return true;
    }
}
