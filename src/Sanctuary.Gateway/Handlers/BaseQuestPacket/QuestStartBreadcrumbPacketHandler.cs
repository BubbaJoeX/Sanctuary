using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Quests;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

/// <summary>
/// The client's StartBreadcrumbQuest: show the way to a quest's current objective. Tracking the quest sends
/// its objective target, which the client's breadcrumb trail asks a path to (ClientPathRequest).
/// </summary>
[PacketHandler]
public static class QuestStartBreadcrumbPacketHandler
{
    private static ILogger _logger = null!;
    private static IQuestManager _questManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(QuestStartBreadcrumbPacketHandler));

        _questManager = serviceProvider.GetRequiredService<IQuestManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!QuestStartBreadcrumbPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(QuestStartBreadcrumbPacket));
            return false;
        }

        _logger.LogTrace("QuestStartBreadcrumbPacket (49/15) received: QuestId={q}", packet.QuestId);

        _questManager.SetActiveQuest(connection.Player, packet.QuestId);

        return true;
    }
}
