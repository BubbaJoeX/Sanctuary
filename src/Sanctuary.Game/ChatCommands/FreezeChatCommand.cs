using System;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Freezes by zeroing MaxMovementSpeed. PlayerUpdatePacketFreeze exists in the client,
/// but its layout is not known yet.
/// </summary>
public class FreezeChatCommand : IChatCommand
{
    private readonly IZoneManager _zoneManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "freeze";
    public string Usage => "<player> | zone on|off";
    public string Description => "Toggles a player's ability to move, or freezes / thaws everyone in your zone but referees (e.g. a race start line). Clears on relog.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public FreezeChatCommand(IZoneManager zoneManager, IChatCommandManager chatCommandManager)
    {
        _zoneManager = zoneManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        if (args.Length == 2 && args[0].Equals("zone", StringComparison.OrdinalIgnoreCase))
            return HandleZone(invoker, args[1]);

        var targetName = string.Join(' ', args);

        if (!_zoneManager.TryGetPlayer(targetName, out var target))
        {
            ChatHelper.SendSystemMessage(invoker, $"No online player named \"{targetName}\" was found.");
            return true;
        }

        if (target.FrozenMovementSpeed is not null)
        {
            Unfreeze(target);

            _chatCommandManager.LogAction(this, invoker, "Unfreeze", targetName, null);
            ChatHelper.SendSystemMessage(invoker, $"{targetName} unfrozen.");
            return true;
        }

        Freeze(target);

        _chatCommandManager.LogAction(this, invoker, "Freeze", targetName, null);
        ChatHelper.SendSystemMessage(invoker, $"{targetName} frozen. Run !{KeyWord} {targetName} again to unfreeze.");

        return true;
    }

    private bool HandleZone(Player invoker, string state)
    {
        bool freeze;

        if (state.Equals("on", StringComparison.OrdinalIgnoreCase))
            freeze = true;
        else if (state.Equals("off", StringComparison.OrdinalIgnoreCase))
            freeze = false;
        else
            return false;

        var players = invoker.Zone.Players
            .Where(player => !player.IsMod && !player.IsAdmin)
            .Where(player => (player.FrozenMovementSpeed is null) == freeze)
            .ToList();

        foreach (var player in players)
        {
            if (freeze)
                Freeze(player);
            else
                Unfreeze(player);
        }

        _chatCommandManager.LogAction(this, invoker, freeze ? "FreezeZone" : "UnfreezeZone", null, $"Players: {players.Count}");
        ChatHelper.SendSystemMessage(invoker, $"{(freeze ? "Froze" : "Unfroze")} {players.Count} player(s) in this zone.");
        return true;
    }

    private static void Freeze(Player target)
    {
        target.FrozenMovementSpeed = target.Stats.TryGetValue(CharacterStatId.MaxMovementSpeed, out var speed) ? speed.Float : 8f;
        target.UpdateCharacterStats(new CharacterStat(CharacterStatId.MaxMovementSpeed, 0f));

        ChatHelper.SendSystemMessage(target, "You have been frozen by a referee.");
    }

    private static void Unfreeze(Player target)
    {
        if (target.FrozenMovementSpeed is not { } previousSpeed)
            return;

        target.FrozenMovementSpeed = null;
        target.UpdateCharacterStats(new CharacterStat(CharacterStatId.MaxMovementSpeed, previousSpeed));

        ChatHelper.SendSystemMessage(target, "You can move again.");
    }
}
