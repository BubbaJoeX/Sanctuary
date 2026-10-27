using System;
using System.Globalization;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Calls Lua functions in a client: the client-side half of the referee tools, and event effects the client's
/// own scripts provide (e.g. CuteMode:Big). Only numbers can be passed; see <see cref="ClientLuaHelper"/>.
/// </summary>
public class LuaChatCommand : IChatCommand
{
    private readonly IZoneManager _zoneManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "lua";
    public string Usage => "tools [referee] | call <function> [x] [y] [z] [player] | fte <Module.Method> [int ...]";
    public string Description => "Calls a Lua function in a client (yours, or a player's) with up to three numbers. " +
        "'tools' reloads the referee client tools (/lua, /dofile, /admin); 'fte' tests the first-time event script packet on you.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public LuaChatCommand(IZoneManager zoneManager, IChatCommandManager chatCommandManager)
    {
        _zoneManager = zoneManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        switch (args[0].ToLowerInvariant())
        {
            case "tools":
                return HandleTools(invoker, args[1..]);

            case "call":
                return HandleCall(invoker, args[1..]);

            case "fte":
                return HandleFte(invoker, args[1..]);

            default:
                return false;
        }
    }

    private bool TryGetTarget(Player invoker, string name, out Player target)
    {
        target = invoker;

        if (name.Length == 0 || _zoneManager.TryGetPlayer(name, out target!))
            return true;

        ChatHelper.SendSystemMessage(invoker, $"No online player named \"{name}\" was found.");
        return false;
    }

    private bool HandleTools(Player invoker, string[] args)
    {
        if (!TryGetTarget(invoker, string.Join(' ', args), out var target))
            return true;

        if (target.ChatCommandRole < ChatCommandRole.Mod)
        {
            ChatHelper.SendSystemMessage(invoker, $"{target.Name.FullName} isn't a referee.");
            return true;
        }

        ClientLuaHelper.LoadRefereeTools(target);

        _chatCommandManager.LogAction(this, invoker, "Tools", target.Name.FullName, null);
        ChatHelper.SendSystemMessage(invoker, $"Loading the referee client tools for {target.Name.FullName}. " +
            "Needs the client mods installed (Resources/Scripts/1).");
        return true;
    }

    private bool HandleCall(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        var functionName = args[0];

        // Up to three numbers, then the rest is a player name.
        var numbers = args.Skip(1).Take(3)
            .TakeWhile(x => float.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && float.IsFinite(value))
            .Select(x => float.Parse(x, NumberStyles.Float, CultureInfo.InvariantCulture))
            .ToArray();

        if (!TryGetTarget(invoker, string.Join(' ', args[(1 + numbers.Length)..]), out var target))
            return true;

        ClientLuaHelper.Call(target, functionName,
            numbers.ElementAtOrDefault(0), numbers.ElementAtOrDefault(1), numbers.ElementAtOrDefault(2));

        var call = $"{functionName}({string.Join(", ", numbers.Select(x => x.ToString(CultureInfo.InvariantCulture)))})";

        _chatCommandManager.LogAction(this, invoker, "Call", target.Name.FullName, call);
        ChatHelper.SendSystemMessage(invoker, $"Called {call} in {target.Name.FullName}'s client. Failures only show in its LuaErrors.log.");
        return true;
    }

    private bool HandleFte(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        var parameters = new System.Collections.Generic.List<int>();

        foreach (var arg in args[1..])
        {
            if (!int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                return false;

            parameters.Add(value);
        }

        invoker.SendTunneled(new FirstTimeEventScriptPacket
        {
            Script = args[0],
            Parameters = parameters
        });

        _chatCommandManager.LogAction(this, invoker, "Fte", invoker.Name.FullName, $"{args[0]}({string.Join(", ", parameters)})");
        ChatHelper.SendSystemMessage(invoker, $"Sent first-time event script {args[0]}({string.Join(", ", parameters)}).");
        return true;
    }
}
