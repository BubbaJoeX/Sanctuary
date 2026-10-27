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

    /// <summary>
    /// ClientMods/lua/client_extensions.lua, installed as Resources/Scripts/2: what Sanctuary adds to the client's
    /// own scripts for everyone (the "Now entering" badges of Sanctuary's own regions). A client without it logs
    /// the failed dofile to its LuaErrors.log and carries on.
    /// </summary>
    public const int ClientExtensionsScript = 2;

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
    /// Loads the client extensions, and for referees the referee client tools, the first time the player finishes
    /// loading into the world this session (the client's Lua state lasts as long as the client, so once is enough).
    /// </summary>
    public static void OnClientFinishedLoading(Player player)
    {
        if (!player.ClientExtensionsLoaded)
        {
            Call(player, "dofileInternalOnlyCommand", ClientExtensionsScript);

            player.ClientExtensionsLoaded = true;
        }

        if (player.RefereeToolsLoaded || player.ChatCommandRole < ChatCommandRole.Mod)
            return;

        LoadRefereeTools(player);
    }
}
