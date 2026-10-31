using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Groups;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

/// <summary>
/// A member opening or closing the start screen's job browser shows as changing job in their group's window.
/// </summary>
[PacketHandler]
public static class GroupPacketChangingJobHandler
{
    private static ILogger _logger = null!;
    private static IGroupManager _groupManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GroupPacketChangingJobHandler));

        _groupManager = serviceProvider.GetRequiredService<IGroupManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!GroupPacketChangingJob.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GroupPacketChangingJob));
            return false;
        }

        if (connection.Player.Group is not null)
            _groupManager.SendToGroup(connection.Player, new GroupPacketChangingJobUpdate { MemberGuid = connection.Player.Guid, IsChangingJob = packet.IsChangingJob });

        return true;
    }
}
