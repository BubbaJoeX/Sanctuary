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
    public string Usage => "[on|off|reset|test [message]]";
    public string Description => "Shows the NPC chat bot's state and which NPC near you would hear you, or toggles it. " +
        "'reset' makes every NPC forget its conversations; 'test' has the NPC that hears you answer now and reports what happened. " +
        "Scripts with an onHear hook answer either way.";
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

            case "test":
                var message = args.Length > 1 ? string.Join(' ', args[1..]) : "Howdy!";

                ChatHelper.SendSystemMessage(invoker, $"Asking the NPC that hears you: \"{message}\"...");
                _npcChatManager.TestAsync(invoker, message).ContinueWith(task =>
                    ChatHelper.SendSystemMessage(invoker, task.IsCompletedSuccessfully ? task.Result : $"The test failed: {task.Exception?.GetBaseException().Message}"));
                return true;

            default:
                return false;
        }

        foreach (var line in _npcChatManager.Describe(invoker))
            ChatHelper.SendSystemMessage(invoker, line);

        return true;
    }
}
