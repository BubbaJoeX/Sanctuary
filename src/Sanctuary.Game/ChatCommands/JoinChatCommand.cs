using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Joins an event that staff opened with !host open, and goes to it.
/// </summary>
public class JoinChatCommand : IChatCommand
{
    private readonly IWorldEventManager _worldEventManager;
    private readonly IZoneManager _zoneManager;

    public string KeyWord => "join";
    public string Usage => "[event]";
    public string Description => "Joins an event that staff are hosting and takes you there. Use it again to go back.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Player;

    public JoinChatCommand(IWorldEventManager worldEventManager, IZoneManager zoneManager)
    {
        _worldEventManager = worldEventManager;
        _zoneManager = zoneManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length > 1)
            return false;

        var joins = _worldEventManager.Joins;
        var key = args.Length == 1 ? args[0].ToLowerInvariant() : null;

        switch (joins.TryFind(key, out var point))
        {
            case EventJoinBook.FindResult.NoneOpen:
                ChatHelper.SendSystemMessage(invoker, "No events are open right now.");
                return true;

            case EventJoinBook.FindResult.NotOpen:
                ChatHelper.SendSystemMessage(invoker, joins.Open.Count == 0
                    ? $"\"{key}\" isn't open. No events are open right now."
                    : $"\"{key}\" isn't open. Open events: {string.Join(", ", joins.Open.Select(x => x.Key))}.");
                return true;

            case EventJoinBook.FindResult.Ambiguous:
                ChatHelper.SendSystemMessage(invoker, $"Several events are open: {string.Join(", ", joins.Open.Select(x => x.Key))}. Type !join <event>.");
                return true;
        }

        if (invoker.IsDead)
        {
            ChatHelper.SendSystemMessage(invoker, "You can't join an event while knocked out.");
            return true;
        }

        if (invoker.IsInCombat)
        {
            ChatHelper.SendSystemMessage(invoker, "Finish your fight before joining an event.");
            return true;
        }

        var firstTime = joins.Join(point!.Key, invoker.Guid, invoker.Name.FullName);

        if (!EventTravel.TrySend(invoker, point))
        {
            ChatHelper.SendSystemMessage(invoker, $"Couldn't take you to \"{point.Key}\". Ask its host for help.");
            return true;
        }

        ChatHelper.SendSystemMessage(invoker, firstTime
            ? $"You joined \"{point.Key}\", hosted by {point.HostName}. Have fun!"
            : $"Back to \"{point.Key}\".");

        if (!firstTime)
            return true;

        // The event zone's scripts can greet newcomers or count them (eventJoin).
        var eventZone = _zoneManager.Zones.FirstOrDefault(zone => zone.DefinitionId == point.ZoneDefinitionId && zone.OwnerId is null);

        if (eventZone is not null)
        {
            var scriptContext = eventZone.GetOrCreateScriptContext();

            if (scriptContext.HasCallbacks("eventJoin"))
                scriptContext.FireEvent("eventJoin", eventZone.NpcContent.WrapPlayer(invoker), point.Key);
        }

        return true;
    }
}
