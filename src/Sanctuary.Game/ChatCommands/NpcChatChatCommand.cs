using System;

using Sanctuary.Game.Conversations;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class NpcChatChatCommand : IChatCommand
{
    private readonly INpcChatManager _npcChatManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "npcchat";
    public string Usage => "[on|off|reset]";
    public string Description => "Shows or toggles NPCs answering what players say near them through the Studio Umbra chat bot. " +
        "'reset' makes every NPC forget its conversations. Scripts with an onHear hook answer either way.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public NpcChatChatCommand(INpcChatManager npcChatManager, IChatCommandManager chatCommandManager)
    {
        _npcChatManager = npcChatManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        switch (args.Length == 0 ? "status" : args[0].ToLowerInvariant())
        {
            case "status":
                break;

            case "on":
            case "off":
                _npcChatManager.BotEnabled = args[0].Equals("on", StringComparison.OrdinalIgnoreCase);
                _chatCommandManager.LogAction(this, invoker, _npcChatManager.BotEnabled ? "On" : "Off", null, null);
                break;

            case "reset":
                _npcChatManager.Reset();
                _chatCommandManager.LogAction(this, invoker, "Reset", null, null);
                ChatHelper.SendSystemMessage(invoker, "NPCs forgot their conversations.");
                return true;

            default:
                return false;
        }

        ChatHelper.SendSystemMessage(invoker, !_npcChatManager.BotConfigured
            ? "The NPC chat bot has no API key (NpcChat:ApiKey in gateway.local.json)."
            : $"The NPC chat bot is {(_npcChatManager.BotEnabled ? "on" : "off")}.");
        return true;
    }
}
