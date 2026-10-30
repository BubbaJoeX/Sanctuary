using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseFishingPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseFishingPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        if (!reader.TryRead(out ushort opCode))
        {
            _logger.LogError("Failed to read opcode from packet. ( Data: {data} )", Convert.ToHexString(reader.Span));
            return false;
        }

        return opCode switch
        {
            FishingPacketRegisterPlayerRequest.OpCode => FishingPacketRegisterPlayerRequestHandler.HandlePacket(connection, reader.Span),
            FishingPacketCastAnimRequest.OpCode => FishingPacketCastAnimRequestHandler.HandlePacket(connection, reader.Span),
            FishingPacketCastRequest.OpCode => FishingPacketCastRequestHandler.HandlePacket(connection, reader.Span),
            FishingPacketReelInRequest.OpCode => FishingPacketReelInRequestHandler.HandlePacket(connection, reader.Span),
            _ => false
        };
    }
}
