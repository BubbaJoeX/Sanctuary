using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;

namespace Sanctuary.Game.Helpers;

/// <summary>
/// Calls Lua functions in a player's client (<see cref="AbilityPacketExecuteClientLua"/>). The client only
/// takes a function name and three numbers, so anything richer lives in a client-side script.
/// </summary>
public static class ClientLuaHelper
{
    /// <summary>
    /// ClientMods/lua/referee_tools.lua, installed as Resources/Scripts/1. The retail client keeps its
    /// startup.lua's dofileInternalOnlyCommand(name), which runs Client.PathScripts .. name; called with this
    /// number it loads the referee tools (/lua, /dofile, /admin, the debug console switch).
    /// </summary>
    public const int RefereeToolsScript = 1;

    public static void Call(Player player, string functionName, float x = 0f, float y = 0f, float z = 0f)
    {
        player.SendTunneled(new AbilityPacketExecuteClientLua
        {
            FunctionName = functionName,
            X = x,
            Y = y,
            Z = z
        });
    }

    public static void LoadRefereeTools(Player player)
    {
        Call(player, "dofileInternalOnlyCommand", RefereeToolsScript);

        player.RefereeToolsLoaded = true;
    }

    /// <summary>
    /// Loads the referee client tools the first time a referee finishes loading into the world this session
    /// (the client's Lua state lasts as long as the client, so once is enough).
    /// </summary>
    public static void OnClientFinishedLoading(Player player)
    {
        if (player.RefereeToolsLoaded || player.ChatCommandRole < ChatCommandRole.Mod)
            return;

        LoadRefereeTools(player);
    }
}
