using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Quests;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

/// <summary>
/// A player riding a moving platform (a house elevator). They move like any other position update, placed
/// by where they stand on the platform, so everyone else sees them ride it.
/// </summary>
[PacketHandler]
public static class PlayerUpdatePacketUpdatePositionOnPlatformHandler
{
    private static ILogger _logger = null!;
    private static IQuestManager _questManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PlayerUpdatePacketUpdatePositionOnPlatformHandler));

        _questManager = serviceProvider.GetRequiredService<IQuestManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!PlayerUpdatePacketUpdatePositionOnPlatform.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}. ( Data: {data} )", nameof(PlayerUpdatePacketUpdatePositionOnPlatform), Convert.ToHexString(data));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PlayerUpdatePacketUpdatePositionOnPlatform), packet);

        var player = connection.Player;
        var update = packet.Update;

        update.Guid = player.Guid;

        // The platform's own position on the server, plus where they stand on it, keeps them on it here.
        if (packet.RelativePosition is { } relativePosition && player.Zone.TryGetNpc(packet.PlatformGuid, out var platform) && platform.IsPlatform)
            update.Position = (platform.Position + relativePosition) with { W = update.Position.W };

        player.PositionState = update.State;
        player.UpdatePosition(update.Position, update.Rotation);

        _questManager.OnEvent(player, new PlayerMovedEvent(player.Position, player.Zone.DefinitionId));

        player.RelayPosition(update);

        return true;
    }
}
