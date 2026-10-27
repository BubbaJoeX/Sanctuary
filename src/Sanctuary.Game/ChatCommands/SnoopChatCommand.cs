using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// The client's Snoop referee permission: read a copy of a player's chat while investigating a report.
/// </summary>
public class SnoopChatCommand : IChatCommand
{
    private readonly IZoneManager _zoneManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "snoop";
    public string Usage => "<player> | off [player] | list";
    public string Description => "Toggles reading a copy of everything a player says, tells included. Stops when either of you logs off.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public SnoopChatCommand(IZoneManager zoneManager, IChatCommandManager chatCommandManager)
    {
        _zoneManager = zoneManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        var subCommand = args[0].ToLowerInvariant();

        if (subCommand == "list" && args.Length == 1)
        {
            var snooped = SnoopedBy(invoker);

            ChatHelper.SendSystemMessage(invoker, snooped.Count == 0
                ? "You aren't snooping anyone."
                : $"Snooping: {string.Join(", ", snooped.Select(x => x.Name.FullName))}");
            return true;
        }

        if (subCommand == "off")
        {
            if (args.Length == 1)
            {
                var snooped = SnoopedBy(invoker);

                foreach (var player in snooped)
                    player.Snoopers.TryRemove(invoker.Guid);

                _chatCommandManager.LogAction(this, invoker, "Off", null, $"Players: {snooped.Count}");
                ChatHelper.SendSystemMessage(invoker, $"Stopped snooping {snooped.Count} player(s).");
                return true;
            }

            if (!TryFindPlayer(invoker, string.Join(' ', args[1..]), out var stopped))
                return true;

            Stop(invoker, stopped);
            return true;
        }

        if (!TryFindPlayer(invoker, string.Join(' ', args), out var target))
            return true;

        if (target.Snoopers.Contains(invoker.Guid))
        {
            Stop(invoker, target);
            return true;
        }

        if (target == invoker)
        {
            ChatHelper.SendSystemMessage(invoker, "You can't snoop yourself.");
            return true;
        }

        if ((target.IsMod || target.IsAdmin) && invoker.ChatCommandRole <= target.ChatCommandRole)
        {
            ChatHelper.SendSystemMessage(invoker, $"You are not authorized to snoop {target.Name.FullName}.");
            return true;
        }

        target.Snoopers.TryAdd(invoker.Guid);

        _chatCommandManager.LogAction(this, invoker, "On", target.Name.FullName, null);
        ChatHelper.SendSystemMessage(invoker, $"Snooping {target.Name.FullName}. Run !{KeyWord} {target.Name.FullName} again to stop.");
        return true;
    }

    private void Stop(Player invoker, Player target)
    {
        target.Snoopers.TryRemove(invoker.Guid);

        _chatCommandManager.LogAction(this, invoker, "Off", target.Name.FullName, null);
        ChatHelper.SendSystemMessage(invoker, $"Stopped snooping {target.Name.FullName}.");
    }

    private System.Collections.Generic.List<Player> SnoopedBy(Player invoker)
    {
        return _zoneManager.Zones
            .SelectMany(zone => zone.Players)
            .Where(player => player.Snoopers.Contains(invoker.Guid))
            .ToList();
    }

    private bool TryFindPlayer(Player invoker, string name, out Player target)
    {
        if (_zoneManager.TryGetPlayer(name, out target!))
            return true;

        ChatHelper.SendSystemMessage(invoker, $"No online player named \"{name}\" was found.");
        return false;
    }
}
