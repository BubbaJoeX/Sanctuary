using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class FishingPacketReelInRequestHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(FishingPacketReelInRequestHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!FishingPacketReelInRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(FishingPacketReelInRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(FishingPacketReelInRequest), packet);

        if (packet.Guid == connection.Player.Guid && connection.Player.Zone is WorldZone { Fishing: { } pond })
            pond.ReelIn(connection.Player);

        return true;
    }
}
