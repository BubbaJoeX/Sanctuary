using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Quests;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PlayerUpdatePacketUpdatePositionHandler
{
    private static ILogger _logger = null!;
    private static IQuestManager _questManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PlayerUpdatePacketUpdatePositionHandler));

        _questManager = serviceProvider.GetRequiredService<IQuestManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!PlayerUpdatePacketUpdatePosition.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PlayerUpdatePacketUpdatePosition));
            return false;
        }

        // _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PlayerUpdatePacketUpdatePosition), packet);

        packet.Guid = connection.Player.Guid;

        connection.Player.PositionState = packet.State;
        connection.Player.UpdatePosition(packet.Position, packet.Rotation);

        _questManager.OnEvent(connection.Player, new PlayerMovedEvent(connection.Player.Position, connection.Player.Zone.DefinitionId));

        if (TryUsePortal(connection.Player))
            return true;

        connection.Player.RelayPosition(packet);

        return true;
    }

    /// <summary>
    /// Walking into a zone portal (e.g. through Lavender Coast Pass) moves the player on to its zone. Arrivals
    /// sit outside the other side's portals, and a short cooldown covers the loading screen.
    /// </summary>
    private static bool TryUsePortal(Player player)
    {
        if (player.Zone is not WorldZone zone || Environment.TickCount64 < player.PortalReadyAt)
            return false;

        var portal = zone.GetPortal(player.Position);

        if (portal is null || portal.ToPosition.Length < 3)
            return false;

        player.PortalReadyAt = Environment.TickCount64 + PortalCooldownMilliseconds;

        if (!player.TeleportToZone(portal.ToZoneId, null, portal.Destination(player.Position.W), portal.DestinationRotation()))
        {
            _logger.LogWarning("Portal {portal} in {zone} could not move {player} to zone {to}.", portal.Comment, zone.Name,
                player.Name.FullName, portal.ToZoneId);
            return false;
        }

        return true;
    }

    private const long PortalCooldownMilliseconds = 8000;
}