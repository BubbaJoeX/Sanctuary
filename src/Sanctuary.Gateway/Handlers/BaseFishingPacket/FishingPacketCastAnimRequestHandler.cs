using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class FishingPacketCastAnimRequestHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(FishingPacketCastAnimRequestHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!FishingPacketCastAnimRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(FishingPacketCastAnimRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(FishingPacketCastAnimRequest), packet);

        if (packet.Guid == connection.Player.Guid && connection.Player.Zone is WorldZone { Fishing: { } pond })
            pond.CastAnimation(connection.Player, packet.Distance, packet.Direction);

        return true;
    }
}
