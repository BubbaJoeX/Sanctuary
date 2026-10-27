using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Configuration;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.Conversations;

public partial class NpcChatManager : INpcChatManager
{
    /// <summary>
    /// Other NPCs within this distance of the speaker are named in its introduction, so it knows its neighbours.
    /// </summary>
    private const float NeighbourRange = 30f;

    private const int MaxNeighbours = 6;

    private readonly ILogger _logger;
    private readonly NpcChatOptions _options;
    private readonly StudioUmbraChatClient? _client;

    // One Studio Umbra conversation per NPC and player, so each NPC remembers what that player said to it.
    private readonly ConcurrentDictionary<(ulong NpcGuid, ulong PlayerGuid), string> _conversations = [];
    private readonly ConcurrentDictionary<ulong, DateTime> _lastHeard = [];
    private readonly ConcurrentDictionary<ulong, byte> _waiting = [];
    private readonly Queue<DateTime> _recentReplies = [];

    public bool BotEnabled { get; set; }

    public bool BotConfigured => _client is not null;

    public NpcChatManager(ILogger<NpcChatManager> logger, NpcChatOptions options)
    {
        _logger = logger;
        _options = options;

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            _client = new StudioUmbraChatClient(options);

        BotEnabled = options.Enabled;
    }

    public void OnPlayerSay(Player player, string message)
    {
        message = message.Trim();

        if (message.Length == 0 || FindListener(player) is not { } npc)
            return;

        // A script that listens decides for itself what to say.
        if (npc.Zone.NpcContent.OnHear(npc, player, message))
            return;

        if (!BotEnabled || _client is null)
            return;

        var now = DateTime.UtcNow;

        if (_lastHeard.TryGetValue(player.Guid, out var last) && now - last < TimeSpan.FromSeconds(_options.PlayerCooldownSeconds))
            return;

        if (_waiting.ContainsKey(player.Guid) || !TryTakeReplySlot(now) || !_waiting.TryAdd(player.Guid, 0))
            return;

        _lastHeard[player.Guid] = now;

        _ = ReplyAsync(npc, player, message);
    }

    public void Reset() => _conversations.Clear();

    private Npc? FindListener(Player player)
    {
        Npc? nearest = null;
        var nearestDistance = _options.HearRange;

        foreach (var npc in player.VisibleNpcs.Values)
        {
            if (!CanHear(npc))
                continue;

            var distance = Distance(npc.Position, player.Position);

            if (distance <= nearestDistance)
            {
                nearest = npc;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private static bool CanHear(Npc npc) => npc is not (Mount or CollectionCoin or CollectionNode or PetTreasureChest)
        && !npc.IsWorldProp && GetName(npc) is not null;

    private bool TryTakeReplySlot(DateTime now)
    {
        lock (_recentReplies)
        {
            while (_recentReplies.Count > 0 && now - _recentReplies.Peek() > TimeSpan.FromMinutes(1))
                _recentReplies.Dequeue();

            if (_recentReplies.Count >= _options.MaxRepliesPerMinute)
                return false;

            _recentReplies.Enqueue(now);
            return true;
        }
    }

    private async Task ReplyAsync(Npc npc, Player player, string message)
    {
        var name = GetName(npc)!;
        var playerName = player.Name.FullName;

        try
        {
            var key = (npc.Guid, player.Guid);
            string content;

            if (_conversations.TryGetValue(key, out var conversationId))
            {
                content = $"{playerName} says: \"{message}\"";
            }
            else
            {
                conversationId = await _client!.StartConversationAsync($"Free Realms: {name} and {playerName}");
                _conversations[key] = conversationId;

                content = BuildIntroduction(npc, player) + $"\n\n{playerName} says to you: \"{message}\"";
            }

            var reply = await _client!.SendAsync(conversationId, content);

            if (reply is null || CleanReply(reply, _options.MaxReplyLength) is not { Length: > 0 } line)
                return;

            npc.FaceTowards(player.Position);
            npc.Say(line);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            _logger.LogWarning("NPC {Npc} ({NpcGuid}) couldn't answer {Player}: {Error}", name, npc.Guid, playerName, ex.Message);
        }
        finally
        {
            _waiting.TryRemove(player.Guid, out _);
        }
    }

    /// <summary>
    /// Who and where the NPC is, sent once at the start of each conversation; Studio Umbra has no system prompt.
    /// </summary>
    private static string BuildIntroduction(Npc npc, Player player)
    {
        var name = GetName(npc)!;
        var title = npc.SubTextNameId != 0 ? GameNameHelper.Get(npc.SubTextNameId) : null;

        var place = npc.Zone.Name;

        if (npc.Zone is WorldZone worldZone && worldZone.GetZoneArea(npc.Position) is { } area
            && (GameNameHelper.Get(area.NameId) ?? GameNameHelper.Get(area.ChatAreaNameId)) is { } areaName)
        {
            place = $"{areaName} in {place}";
        }

        var neighbours = player.VisibleNpcs.Values
            .Where(other => other != npc && CanHear(other) && Distance(other.Position, npc.Position) <= NeighbourRange)
            .Select(GetName)
            .Distinct()
            .Take(MaxNeighbours)
            .ToList();

        var builder = new StringBuilder();

        builder.Append($"You are {name}");

        if (!string.IsNullOrWhiteSpace(title))
            builder.Append($", {title}");

        builder.Append($", a character in Free Realms, a cheerful family-friendly fantasy world. You are at {place}.");

        if (neighbours.Count > 0)
            builder.Append($" Nearby are {string.Join(", ", neighbours)}.");

        if (npc.Conversation?.GetNode(npc.Conversation.Start)?.Text is { Length: > 0 } greeting)
            builder.Append($" When someone talks to you, you usually say: \"{greeting}\"");

        builder.Append($"\n\nPlayers walk up and talk to you out loud. Stay in character as {name} and never say you are an AI, a bot or part of a game. ");
        builder.Append("Answer in one or two short sentences of plain speech, under 200 characters, with no actions, asterisks, emoji, markdown or quotation marks. ");
        builder.Append("Keep everything kind and suitable for children. If asked about things outside this world, steer back to the world around you.");

        return builder.ToString();
    }

    /// <summary>
    /// Turns the bot's answer into one line of NPC speech: no reasoning, actions or wrapping quotes, and cut at a
    /// sentence end near the limit.
    /// </summary>
    public static string CleanReply(string reply, int maxLength)
    {
        var line = ThinkBlock().Replace(reply, " ");
        line = Action().Replace(line, " ");
        line = Whitespace().Replace(line, " ").Trim().Trim('"', '“', '”').Trim();

        if (line.Length <= maxLength)
            return line;

        var cut = line[..maxLength];
        var sentenceEnd = cut.LastIndexOfAny(['.', '!', '?']);

        return sentenceEnd >= maxLength / 2 ? cut[..(sentenceEnd + 1)] : cut.TrimEnd() + "...";
    }

    private static string? GetName(Npc npc) => !string.IsNullOrWhiteSpace(npc.Name) ? npc.Name
        : npc.NameId != 0 ? GameNameHelper.Get(npc.NameId) : null;

    private static float Distance(Vector4 a, Vector4 b) => Vector3.Distance(new Vector3(a.X, a.Y, a.Z), new Vector3(b.X, b.Y, b.Z));

    [GeneratedRegex(@"<think>.*?(</think>|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ThinkBlock();

    [GeneratedRegex(@"\*[^*]*\*")]
    private static partial Regex Action();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
