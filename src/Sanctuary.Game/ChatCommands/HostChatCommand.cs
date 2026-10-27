using System;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions.Rewards;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Packet;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Runs a player event for event staff: opens it for players to !join at the host's spot, gathers everyone who
/// joined, and hands out prizes to them (see <see cref="EventJoinBook"/>).
/// </summary>
public class HostChatCommand : IChatCommand
{
    private const int MaxKeyLength = 32;
    private const int MaxPrizeQuantity = 999;
    private const int MaxPrizeCoins = 100_000;

    private readonly IWorldEventManager _worldEventManager;
    private readonly IZoneManager _zoneManager;
    private readonly IRewardManager _rewardManager;
    private readonly IResourceManager _resourceManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "host";
    public string Usage => "open <event> | close <event> | end <event> | list | who <event> | summon <event> | prize <event> <itemId> [qty] | coins <event> <amount>";
    public string Description => "Hosts a player event: 'open' lets players !join at your spot, 'summon' gathers everyone who joined, 'prize' and 'coins' reward them, and 'end' closes it and forgets who joined.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public HostChatCommand(IWorldEventManager worldEventManager, IZoneManager zoneManager, IRewardManager rewardManager,
        IResourceManager resourceManager, IChatCommandManager chatCommandManager)
    {
        _worldEventManager = worldEventManager;
        _zoneManager = zoneManager;
        _rewardManager = rewardManager;
        _resourceManager = resourceManager;
        _chatCommandManager = chatCommandManager;
    }

    private EventJoinBook Joins => _worldEventManager.Joins;

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        var action = args[0].ToLowerInvariant();

        if (action == "list")
            return HandleList(invoker);

        if (args.Length < 2)
            return false;

        var key = args[1].ToLowerInvariant();

        if (key.Length > MaxKeyLength)
        {
            ChatHelper.SendSystemMessage(invoker, $"Event names are at most {MaxKeyLength} characters.");
            return true;
        }

        return action switch
        {
            "open" => HandleOpen(invoker, key),
            "close" => HandleClose(invoker, key),
            "end" => HandleEnd(invoker, key),
            "who" => HandleWho(invoker, key),
            "summon" => HandleSummon(invoker, key),
            "prize" => HandlePrize(invoker, key, args[2..]),
            "coins" => HandleCoins(invoker, key, args[2..]),
            _ => false
        };
    }

    private bool HandleList(Player invoker)
    {
        var open = Joins.Open;

        ChatHelper.SendSystemMessage(invoker, open.Count == 0
            ? "No events are open. Stand where players should gather and use !host open <event>."
            : $"Open events: {string.Join(", ", open.Select(point => $"{point.Key} ({Joins.Participants(point.Key).Count} joined, hosted by {point.HostName})"))}");

        return true;
    }

    private bool HandleOpen(Player invoker, string key)
    {
        // !join may send players across zones, and only world zones take anyone without an owner.
        if (invoker.Zone.OwnerId is not null
            || !_resourceManager.Zones.TryGetValue(invoker.Zone.DefinitionId, out var zoneDefinition)
            || zoneDefinition is not WorldZoneDefinition)
        {
            ChatHelper.SendSystemMessage(invoker, "Open events from a world zone, where everyone can join you.");
            return true;
        }

        var reopened = Joins.TryFind(key, out _) == EventJoinBook.FindResult.Found;

        Joins.OpenJoin(new EventJoinBook.JoinPoint(key, invoker.Zone.DefinitionId, invoker.Position, invoker.Rotation,
            invoker.Name.FullName));

        if (reopened)
        {
            ChatHelper.SendSystemMessage(invoker, $"\"{key}\" now gathers players here.");
        }
        else
        {
            var message = $"{invoker.Name.FullName} is hosting \"{key}\" in {invoker.Zone.Name}! Type !join to take part.";
            var banner = new BroadcastPacketZone { Message = message };

            foreach (var player in _zoneManager.Zones.SelectMany(zone => zone.Players))
            {
                ChatHelper.SendSystemMessage(player, $"[Event] {message}");
                player.SendTunneled(banner);
            }
        }

        _chatCommandManager.LogAction(this, invoker, "Open", key, $"Zone: {invoker.Zone.DefinitionId}, Position: {invoker.Position}");

        return true;
    }

    private bool HandleClose(Player invoker, string key)
    {
        ChatHelper.SendSystemMessage(invoker, Joins.Close(key)
            ? $"\"{key}\" is closed to new players. {Joins.Participants(key).Count} joined; prizes still reach them."
            : $"\"{key}\" isn't open.");

        _chatCommandManager.LogAction(this, invoker, "Close", key, null);

        return true;
    }

    private bool HandleEnd(Player invoker, string key)
    {
        var joined = Joins.End(key);

        foreach (var guid in joined.Keys)
        {
            if (_zoneManager.TryGetPlayer(guid, out var player))
                ChatHelper.SendSystemMessage(player, $"[Event] \"{key}\" is over. Thanks for joining!");
        }

        ChatHelper.SendSystemMessage(invoker, $"Ended \"{key}\" for {joined.Count} players.");

        _chatCommandManager.LogAction(this, invoker, "End", key, $"Joined: {joined.Count}");

        return true;
    }

    private bool HandleWho(Player invoker, string key)
    {
        var joined = Joins.Participants(key);

        if (joined.Count == 0)
        {
            ChatHelper.SendSystemMessage(invoker, $"Nobody has joined \"{key}\".");
            return true;
        }

        var names = joined
            .OrderBy(entry => entry.Value)
            .Select(entry => _zoneManager.TryGetPlayer(entry.Key, out _) ? entry.Value : $"{entry.Value} (offline)");

        ChatHelper.SendSystemMessage(invoker, $"{joined.Count} joined \"{key}\": {string.Join(", ", names)}");

        return true;
    }

    private bool HandleSummon(Player invoker, string key)
    {
        if (Joins.TryFind(key, out var point) != EventJoinBook.FindResult.Found)
        {
            ChatHelper.SendSystemMessage(invoker, $"\"{key}\" isn't open, so it has nowhere to gather players.");
            return true;
        }

        var moved = 0;

        foreach (var guid in Joins.Participants(key).Keys)
        {
            if (!_zoneManager.TryGetPlayer(guid, out var player) || player.IsDead)
                continue;

            if (EventTravel.TrySend(player, point))
                moved++;
        }

        ChatHelper.SendSystemMessage(invoker, $"Gathered {moved} players for \"{key}\".");

        _chatCommandManager.LogAction(this, invoker, "Summon", key, $"Moved: {moved}");

        return true;
    }

    private bool HandlePrize(Player invoker, string key, string[] args)
    {
        if (args.Length is < 1 or > 2 || !int.TryParse(args[0], out var itemDefinitionId))
            return false;

        var quantity = 1;

        if (args.Length == 2 && (!int.TryParse(args[1], out quantity) || quantity is < 1 or > MaxPrizeQuantity))
        {
            ChatHelper.SendSystemMessage(invoker, $"Prizes are 1 to {MaxPrizeQuantity} of an item.");
            return true;
        }

        if (!_resourceManager.ClientItemDefinitions.ContainsKey(itemDefinitionId))
        {
            ChatHelper.SendSystemMessage(invoker, $"There's no item {itemDefinitionId}.");
            return true;
        }

        var given = RewardOnline(key, player => _rewardManager.TryGrantItem(player, itemDefinitionId, 0, quantity, invoker.Guid));

        ChatHelper.SendSystemMessage(invoker, $"Gave {quantity} of item {itemDefinitionId} to {given} players in \"{key}\".");

        _chatCommandManager.LogAction(this, invoker, "Prize", key, $"Item: {itemDefinitionId}, Quantity: {quantity}, Players: {given}");

        return true;
    }

    private bool HandleCoins(Player invoker, string key, string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var amount))
            return false;

        if (amount is < 1 or > MaxPrizeCoins)
        {
            ChatHelper.SendSystemMessage(invoker, $"Coin prizes are 1 to {MaxPrizeCoins}.");
            return true;
        }

        var given = RewardOnline(key, player => _rewardManager.TryGrantCurrency(player, CurrencyType.Coins, amount));

        ChatHelper.SendSystemMessage(invoker, $"Gave {amount} coins to {given} players in \"{key}\".");

        _chatCommandManager.LogAction(this, invoker, "Coins", key, $"Amount: {amount}, Players: {given}");

        return true;
    }

    /// <summary>
    /// Runs <paramref name="grant"/> for everyone online who joined. Returns how many it worked for.
    /// </summary>
    private int RewardOnline(string key, Func<Player, bool> grant)
    {
        var given = 0;

        foreach (var guid in Joins.Participants(key).Keys)
        {
            if (_zoneManager.TryGetPlayer(guid, out var player) && grant(player))
                given++;
        }

        return given;
    }
}
