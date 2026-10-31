using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Groups;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

/// <summary>
/// A ping on the minimap shows on the minimap of everyone in the pinger's group (or just theirs, alone).
/// </summary>
[PacketHandler]
public static class GroupPacketMapPingHandler
{
    private static ILogger _logger = null!;
    private static IGroupManager _groupManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GroupPacketMapPingHandler));

        _groupManager = serviceProvider.GetRequiredService<IGroupManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!GroupPacketMapPing.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GroupPacketMapPing));
            return false;
        }

        _groupManager.SendToGroup(connection.Player, new GroupPacketMapPing { Guid = connection.Player.Guid, X = packet.X, Z = packet.Z });

        return true;
    }
}
