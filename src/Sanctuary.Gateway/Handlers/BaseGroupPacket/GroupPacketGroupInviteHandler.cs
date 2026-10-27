using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Groups;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class GroupPacketGroupInviteHandler
{
    private static ILogger _logger = null!;
    private static IGroupManager _groupManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GroupPacketGroupInviteHandler));

        _groupManager = serviceProvider.GetRequiredService<IGroupManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!GroupPacketGroupInvite.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GroupPacketGroupInvite));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GroupPacketGroupInvite), packet);

        // The client fills in only the name it was given (typed, or the clicked player's).
        _groupManager.Invite(connection.Player, packet.Data.InviteeGuid, packet.Data.InviteeName.FullName);

        return true;
    }
}
