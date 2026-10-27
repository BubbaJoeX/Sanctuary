using System;
using System.Numerics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketZoneTeleportRequestHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IZoneManager _zoneManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketZoneTeleportRequestHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!PacketZoneTeleportRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketZoneTeleportRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketZoneTeleportRequest), packet);

        if (!_resourceManager.PointOfInterests.TryGetValue(packet.Id, out var pointOfInterest))
        {
            _logger.LogError("Invalid point of interest.");
            return true;
        }

        var rotationX = MathF.Cos(pointOfInterest.Heading);
        var rotationZ = MathF.Sin(pointOfInterest.Heading);

        var position = pointOfInterest.SpawnPosition;
        var rotation = new Quaternion(rotationZ, 0f, rotationX, 0f);

        // A warpstone in another world zone (Lavender Coast's, seen from FabledRealms, or the other way round).
        var zoneId = pointOfInterest.ZoneId != 0 ? pointOfInterest.ZoneId : _zoneManager.StartingZone.DefinitionId;
        if (connection.Player.Zone is WorldZone && connection.Player.Zone.DefinitionId != zoneId)
        {
            if (!connection.Player.TeleportToZone(zoneId, null, position, rotation))
                _logger.LogWarning("Failed to move {player} to zone {zone} for point of interest {id}.",
                    connection.Player.Name.FullName, zoneId, pointOfInterest.Id);
            return true;
        }

        connection.Player.UpdatePosition(position, rotation);

        connection.Player.UpdatePointOfInterest();

        var clientUpdatePacketUpdateLocation = new ClientUpdatePacketUpdateLocation
        {
            Position = position,
            Rotation = rotation,
            Teleport = true
        };

        connection.SendTunneled(clientUpdatePacketUpdateLocation);

        return true;
    }
}
