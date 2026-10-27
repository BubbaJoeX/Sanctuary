using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class GiveChatCommand : IChatCommand
{
    private const int MaxQuantity = 999;

    private readonly IChatCommandManager _chatCommandManager;
    private readonly IRewardManager _rewardManager;
    private readonly IResourceManager _resourceManager;
    private readonly IZoneManager _zoneManager;

    public string KeyWord => "give";
    public string Usage => "<item_id> [quantity] [tint_id] [player]";
    public string Description => "Referee only. Puts items in your inventory, or in an online player's (event prizes).";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public GiveChatCommand(IChatCommandManager chatCommandManager, IRewardManager rewardManager, IResourceManager resourceManager,
        IZoneManager zoneManager)
    {
        _chatCommandManager = chatCommandManager;
        _rewardManager = rewardManager;
        _resourceManager = resourceManager;
        _zoneManager = zoneManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out var itemDefinitionId))
            return false;

        // Up to three numbers, then the rest is a player name (names have spaces).
        var numbers = args.Skip(1).Take(2).TakeWhile(x => int.TryParse(x, out _)).Count();
        var targetName = string.Join(' ', args[(1 + numbers)..]);

        var quantity = 1;
        var tint = 0;

        if (numbers >= 1 && (!int.TryParse(args[1], out quantity) || quantity <= 0 || quantity > MaxQuantity))
            return false;

        if (numbers == 2 && !int.TryParse(args[2], out tint))
            return false;

        var target = invoker;

        if (targetName.Length > 0 && !_zoneManager.TryGetPlayer(targetName, out target!))
        {
            ChatHelper.SendSystemMessage(invoker, $"No online player named \"{targetName}\" was found.");
            return true;
        }

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var itemDefinition))
        {
            ChatHelper.SendSystemMessage(invoker, $"Unknown item {itemDefinitionId}.");
            return true;
        }

        if (!_rewardManager.TryGrantItem(target, itemDefinitionId, tint, quantity))
        {
            ChatHelper.SendSystemMessage(invoker, $"Couldn't give item {itemDefinitionId}.");
            return true;
        }

        var itemName = GameNameHelper.Get(itemDefinition.NameId) ?? itemDefinition.Comment ?? $"item {itemDefinitionId}";

        _chatCommandManager.LogAction(this, invoker, "Give Item", target == invoker ? $"#{itemDefinitionId}" : target.Name.FullName,
            $"item={itemDefinitionId}, quantity={quantity}, tint={tint}");

        if (target != invoker)
        {
            ChatHelper.SendSystemMessage(target, $"A referee gave you {quantity}x {itemName}.");
            ChatHelper.SendSystemMessage(invoker, $"Gave {target.Name.FullName} {quantity}x {itemName}.");
            return true;
        }

        ChatHelper.SendSystemMessage(invoker, $"Gave {quantity}x {itemName}.");
        return true;
    }
}
