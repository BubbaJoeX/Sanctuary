using System;
using System.Linq;

using Sanctuary.Core.Helpers;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Edits the Referee Picks cards on the welcome screen (see <see cref="IRefereePickManager"/>).
/// </summary>
public class PickChatCommand : IChatCommand
{
    private readonly IResourceManager _resourceManager;
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IRefereePickManager _refereePickManager;

    public string KeyWord => "pick";
    public string Usage => "| add <teleport <poi id>|shop|houses|friends|journal|mystuff|achievements> <title> | <text> | remove <n> | clear | reset";
    public string Description => "Lists or edits the Referee Picks on the welcome screen, which the admin site edits too. Changes reach everyone online the next time they open it.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public PickChatCommand(IResourceManager resourceManager, IChatCommandManager chatCommandManager, IRefereePickManager refereePickManager)
    {
        _resourceManager = resourceManager;
        _chatCommandManager = chatCommandManager;
        _refereePickManager = refereePickManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
        {
            var picks = _refereePickManager.Current;

            ChatHelper.SendSystemMessage(invoker, picks.Count == 0
                ? "No Referee Picks are set."
                : string.Join('\n', picks.Select((pick, i) => $"{i + 1}. {pick.Title} ({pick.Action}{(pick.Param1 != 0 ? $" {pick.Param1}" : "")})")));
            return true;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "add" when args.Length > 2:
                return Add(invoker, args[1..]);

            case "remove" when args.Length == 2 && int.TryParse(args[1], out var number):
                if (!_refereePickManager.TryRemoveAt(number - 1))
                {
                    ChatHelper.SendSystemMessage(invoker, $"There is no pick {number}.");
                    return true;
                }

                Changed(invoker, "Remove", $"Pick: {number}");
                return true;

            case "clear":
                _refereePickManager.Clear();
                Changed(invoker, "Clear", null);
                return true;

            case "reset":
                _refereePickManager.Reset(invoker.CharacterId);
                Changed(invoker, "Reset", null);
                return true;
        }

        return false;
    }

    private bool Add(Player invoker, string[] args)
    {
        if (!RefereePickHelper.Kinds.TryGetValue(args[0], out var kind))
            return false;

        var param1 = 0;
        var rest = args[1..];

        if (kind.Action == "Teleport")
        {
            if (rest.Length < 2 || !int.TryParse(rest[0], out param1))
                return false;

            if (!_resourceManager.PointOfInterests.ContainsKey(param1))
            {
                ChatHelper.SendSystemMessage(invoker, $"There is no point of interest {param1}.");
                return true;
            }

            rest = rest[1..];
        }

        var parts = string.Join(' ', rest).Split('|', 2, StringSplitOptions.TrimEntries);

        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            return false;

        if (parts[0].Length > RefereePickHelper.MaxTitleLength || parts[1].Length > RefereePickHelper.MaxTextLength)
        {
            ChatHelper.SendSystemMessage(invoker, $"Keep the title to {RefereePickHelper.MaxTitleLength} characters and the text to {RefereePickHelper.MaxTextLength}.");
            return true;
        }

        if (!_refereePickManager.TryAdd(new WelcomeCard(parts[0], parts[1], kind.Action, param1, kind.Icon), invoker.CharacterId))
        {
            ChatHelper.SendSystemMessage(invoker, $"The welcome screen holds {RefereePickHelper.MaxPicks} picks; remove one first.");
            return true;
        }

        Changed(invoker, "Add", $"Title: \"{parts[0]}\"");
        return true;
    }

    private void Changed(Player invoker, string action, string? detail)
    {
        _chatCommandManager.LogAction(this, invoker, action, null, detail);
        ChatHelper.SendSystemMessage(invoker, $"Referee Picks updated ({_refereePickManager.Current.Count} of {RefereePickHelper.MaxPicks}).");
    }
}
