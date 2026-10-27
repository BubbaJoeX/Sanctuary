using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Trading;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class TradeCoinCountPacketHandler
{
    private static ILogger _logger = null!;
    private static ITradeManager _tradeManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(TradeCoinCountPacketHandler));

        _tradeManager = serviceProvider.GetRequiredService<ITradeManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!TradeCoinCountPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(TradeCoinCountPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(TradeCoinCountPacket), packet);

        _tradeManager.OfferCoins(connection.Player, packet.SessionId, packet.Coins);

        return true;
    }
}
