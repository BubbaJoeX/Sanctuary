using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Trading;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class TradeCancelPacketHandler
{
    private static ILogger _logger = null!;
    private static ITradeManager _tradeManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(TradeCancelPacketHandler));

        _tradeManager = serviceProvider.GetRequiredService<ITradeManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!TradeCancelPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(TradeCancelPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(TradeCancelPacket), packet);

        _tradeManager.Cancel(connection.Player, packet.SessionId);

        return true;
    }
}
