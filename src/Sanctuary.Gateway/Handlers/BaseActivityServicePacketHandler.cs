using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Fishing;
using Sanctuary.Game.Minigames;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

/// <summary>
/// Activity service requests from the Games window: a list refresh re-sends the activities, and joining one opens
/// its minigame's start screen (or goes fishing). Anything else is logged until it's implemented.
/// </summary>
[PacketHandler]
public static class BaseActivityServicePacketHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseActivityServicePacketHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (BaseActivityServicePacket.TryReadSubOps(data, out var serviceSubOpCode, out var activitySubOpCode)
            && serviceSubOpCode == BaseActivityServicePacket.ActivitySubOpCode
            && activitySubOpCode == BaseActivityServicePacket.ActivityListRefreshRequest)
        {
            BaseZone.SendActivities(connection.Player);
            return true;
        }

        if (serviceSubOpCode == BaseActivityServicePacket.ActivitySubOpCode
            && activitySubOpCode == BaseActivityServicePacket.JoinActivityRequest
            && data.Length >= 8)
        {
            var activityId = BitConverter.ToInt32(data[4..]);
            _logger.LogInformation("{player} joined activity {activity}.", connection.Player.Name.FullName, activityId);

            // A fishing pond's entry takes the player there; anything else is a minigame, whose start screen opens.
            if (_resourceManager.Fishing.TryGetPondByActivity(activityId, out var pond))
                FishingPond.GoFishing(connection.Player, pond);
            else
                MinigameManager.Join(connection.Player, activityId);

            return true;
        }

        _logger.LogInformation("{player} sent an activity packet. ( Data: {data} )", connection.Player.Name.FullName, Convert.ToHexString(data));
        return true;
    }
}
