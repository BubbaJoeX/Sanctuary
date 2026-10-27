using System;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;

namespace Sanctuary.Game.ChatCommands;

public class BroadcastChatCommand : IChatCommand
{
    private readonly IZoneManager _zoneManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "broadcast";
    public string Usage => "[zone] [banner] <message>";
    public string Description => "Sends a system message to every player online, or only to players in your zone. 'banner' also shows it across their screen.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public BroadcastChatCommand(IZoneManager zoneManager, IChatCommandManager chatCommandManager)
    {
        _zoneManager = zoneManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        var zoneOnly = false;
        var banner = false;

        // Flags in either order before the message.
        while (args.Length > 1)
        {
            if (!zoneOnly && args[0].Equals("zone", StringComparison.OrdinalIgnoreCase))
                zoneOnly = true;
            else if (!banner && args[0].Equals("banner", StringComparison.OrdinalIgnoreCase))
                banner = true;
            else
                break;

            args = args[1..];
        }

        if (args.Length == 0)
            return false;

        var message = string.Join(' ', args);

        var recipients = zoneOnly
            ? invoker.Zone.Players.ToList()
            : _zoneManager.Zones.SelectMany(zone => zone.Players).ToList();

        var bannerPacket = banner ? new BroadcastPacketZone { Message = message } : null;

        foreach (var player in recipients)
        {
            ChatHelper.SendSystemMessage(player, $"[Broadcast] {message}");

            if (bannerPacket is not null)
                player.SendTunneled(bannerPacket);
        }

        _chatCommandManager.LogAction(this, invoker, zoneOnly ? "ZoneBroadcast" : "Broadcast", null,
            $"Recipients: {recipients.Count}, Banner: {banner}, Message: \"{message}\"");

        return true;
    }
}
