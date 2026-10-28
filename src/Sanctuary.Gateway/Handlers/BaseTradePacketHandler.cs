using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseTradePacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseTradePacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        if (!reader.TryRead(out byte opCode))
        {
            _logger.LogError("Failed to read opcode from packet. ( Data: {data} )", Convert.ToHexString(reader.Span));
            return false;
        }

        return opCode switch
        {
            TradeInviteReplyPacket.OpCode => TradeInviteReplyPacketHandler.HandlePacket(connection, reader.Span),
            TradeAcceptPacket.OpCode => TradeAcceptPacketHandler.HandlePacket(connection, reader.Span),
            TradeCancelPacket.OpCode => TradeCancelPacketHandler.HandlePacket(connection, reader.Span),
            TradeLockPacket.OpCode => TradeLockPacketHandler.HandlePacket(connection, reader.Span),
            TradeOfferItemPacket.OpCode => TradeOfferItemPacketHandler.HandlePacket(connection, reader.Span),
            TradeCoinCountPacket.OpCode => TradeCoinCountPacketHandler.HandlePacket(connection, reader.Span),
            _ => false
        };
    }
}
