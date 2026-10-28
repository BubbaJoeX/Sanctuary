using System;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Minigames;

namespace Sanctuary.Game.ChatCommands;

public class MinigameChatCommand : IChatCommand
{
    private const int ListLimit = 25;

    private readonly IZoneManager _zoneManager;
    private readonly IResourceManager _resourceManager;

    public string KeyWord => "minigame";
    public string Usage => "<id> [player] | list [game or name] | close [player]";
    public string Description => "Opens a minigame's start screen (Resources/Minigames), lists them, or closes the one open.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public MinigameChatCommand(IZoneManager zoneManager, IResourceManager resourceManager)
    {
        _zoneManager = zoneManager;
        _resourceManager = resourceManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        if (args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            var filter = string.Join(' ', args[1..]);

            var matches = _resourceManager.Minigames.Values
                .Where(x => filter.Length == 0
                    || x.Game.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || x.Comment.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Game)
                .ThenBy(x => x.Id)
                .ToList();

            if (matches.Count == 0)
            {
                ChatHelper.SendSystemMessage(invoker, filter.Length == 0 ? "No minigames are loaded." : $"No minigame matches \"{filter}\".");
                return true;
            }

            foreach (var definition in matches.Take(ListLimit))
            {
                ChatHelper.SendSystemMessage(invoker, $"{definition.Id}: {definition.Comment} ({definition.Game}"
                    + (MinigameManager.CanRun(definition) ? ")" : ", no server)"));
            }

            if (matches.Count > ListLimit)
                ChatHelper.SendSystemMessage(invoker, $"... and {matches.Count - ListLimit} more. Narrow it with !minigame list <game or name>.");

            return true;
        }

        if (args[0].Equals("close", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetTarget(invoker, args[1..], out var target))
                return true;

            if (MinigameManager.GetSession(target) is null)
            {
                ChatHelper.SendSystemMessage(invoker, $"{target.Name.FullName} has no minigame open.");
                return true;
            }

            MinigameManager.Close(target);
            ChatHelper.SendSystemMessage(invoker, $"Closed {target.Name.FullName}'s minigame.");
            return true;
        }

        if (!int.TryParse(args[0], out var minigameId))
            return false;

        if (!_resourceManager.Minigames.TryGetValue(minigameId, out var minigame))
        {
            ChatHelper.SendSystemMessage(invoker, $"There's no minigame {minigameId}. Try !minigame list.");
            return true;
        }

        if (!TryGetTarget(invoker, args[1..], out var player))
            return true;

        if (!MinigameManager.Join(player, minigameId))
        {
            ChatHelper.SendSystemMessage(invoker, $"The server can't run {minigame.Comment} ({minigame.Game}).");
            return true;
        }

        ChatHelper.SendSystemMessage(invoker, $"Opened {minigame.Comment} for {player.Name.FullName}.");
        return true;
    }

    private bool TryGetTarget(Player invoker, string[] args, out Player target)
    {
        target = invoker;

        if (args.Length == 0)
            return true;

        var targetName = string.Join(' ', args);

        if (_zoneManager.TryGetPlayer(targetName, out var found))
        {
            target = found;
            return true;
        }

        ChatHelper.SendSystemMessage(invoker, $"No online player named \"{targetName}\" was found.");
        return false;
    }
}
