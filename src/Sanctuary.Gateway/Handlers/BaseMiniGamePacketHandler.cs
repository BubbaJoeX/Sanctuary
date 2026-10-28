using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game.Minigames;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

/// <summary>
/// Minigame requests: starting a game, its Flash messages, and closing it, for <see cref="MinigameManager"/>.
/// Every other minigame packet is logged in full, so the remaining layouts can be confirmed against the client.
/// </summary>
[PacketHandler]
public static class BaseMiniGamePacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseMiniGamePacketHandler));

        MinigameManager.ConfigureServices(serviceProvider);
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        var data = Convert.ToHexString(reader.Span);

        // The tunnel handler has read the opcode already; the header reader starts from it.
        reader.Reset();

        if (BaseMiniGamePacket.TryReadHeader(ref reader, out var subOpCode, out var stageId, out _))
        {
            switch (subOpCode)
            {
                case BaseMiniGamePacket.RequestStartGame:
                    MinigameManager.RequestStart(connection.Player, stageId);
                    return true;

                case BaseMiniGamePacket.Payload when reader.TryRead(out string payload):
                    MinigameManager.HandlePayload(connection.Player, stageId, payload);
                    return true;

                case BaseMiniGamePacket.RequestCancelGame:
                case BaseMiniGamePacket.RequestLeaveGame:
                    MinigameManager.Cancel(connection.Player);
                    return true;
            }
        }

        _logger.LogInformation("{player} sent a minigame packet. ( Data: {data} )", connection.Player.Name.FullName, data);

        return true;
    }
}
