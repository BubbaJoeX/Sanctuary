using System;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// The retail client has no fly packet: its free-fly controller (ControllerFreeFly) is a detached camera.
/// This turns on gliding with no fall speed and a high jump instead, so a jump followed by a glide holds height.
/// </summary>
public class FlyChatCommand : IChatCommand
{
    private static readonly CharacterStat[] FlyStats =
    [
        CharacterStats.GlideEnabled.Set(1),
        CharacterStats.GlideFallSpeed.Set(0f),
        CharacterStats.GlideFallTime.Set(1000f),
        CharacterStats.GlideDefaultForwardSpeed.Set(20f),
        CharacterStats.GlideMinForwardSpeed.Set(4f),
        CharacterStats.GlideMaxForwardSpeed.Set(40f),
        CharacterStats.GlideAccel.Set(10f),
        CharacterStats.JumpHeight.Set(20f)
    ];

    private readonly IZoneManager _zoneManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "fly";
    public string Usage => "[on|off] [player]";
    public string Description => "Toggles flying: gliding that never sinks plus a high jump. Jump for height, then glide. Clears on relog.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public FlyChatCommand(IZoneManager zoneManager, IChatCommandManager chatCommandManager)
    {
        _zoneManager = zoneManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        bool? turnOn = null;

        if (args.Length > 0 && args[0].Equals("on", StringComparison.OrdinalIgnoreCase))
            turnOn = true;
        else if (args.Length > 0 && args[0].Equals("off", StringComparison.OrdinalIgnoreCase))
            turnOn = false;

        var target = invoker;
        var targetName = string.Join(' ', args[(turnOn is null ? 0 : 1)..]);

        if (targetName.Length > 0 && !_zoneManager.TryGetPlayer(targetName, out target))
        {
            ChatHelper.SendSystemMessage(invoker, $"No online player named \"{targetName}\" was found.");
            return true;
        }

        var flying = target.FlyRestoreStats is not null;

        if ((turnOn ?? !flying) == flying)
        {
            ChatHelper.SendSystemMessage(invoker, $"{target.Name.FullName} is {(flying ? "already" : "not")} flying.");
            return true;
        }

        if (flying)
        {
            target.UpdateCharacterStats(target.FlyRestoreStats!);
            target.FlyRestoreStats = null;
        }
        else
        {
            target.FlyRestoreStats = FlyStats
                .Select(x => target.Stats.TryGetValue(x.Id, out var current) ? current : x.IsFloat ? x.Set(0f) : x.Set(0))
                .ToArray();

            target.UpdateCharacterStats(FlyStats);
        }

        _chatCommandManager.LogAction(this, invoker, flying ? "Off" : "On", target.Name.FullName, null);

        if (target != invoker)
            ChatHelper.SendSystemMessage(invoker, $"Flying turned {(flying ? "off" : "on")} for {target.Name.FullName}.");

        ChatHelper.SendSystemMessage(target, flying
            ? "Flying off."
            : "Flying on: jump for height, then glide. Mounting or dismounting resets it.");

        return true;
    }
}
