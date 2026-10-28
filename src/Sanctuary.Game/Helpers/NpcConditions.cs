using System;
using System.Text.Json.Serialization;

using Sanctuary.Game.Entities;

namespace Sanctuary.Game.Helpers;

/// <summary>
/// Who sees a world NPC or prop, and when: set from the zone editor and saved with its other
/// <see cref="NpcProperties"/>. Every condition that is set must hold. Referees in god mode see it regardless,
/// so they can still find and edit it, and its event (DbWorldNpc.EventKey) decides whether it's spawned at all.
/// </summary>
public sealed class NpcConditions
{
    public const string QuestDone = "done";
    public const string QuestActive = "active";
    public const string QuestNotDone = "not-done";
    public const string QuestNotStarted = "not-started";

    public static readonly string[] QuestStates = [QuestDone, QuestActive, QuestNotDone, QuestNotStarted];

    /// <summary>
    /// Game-clock hours (<see cref="GameClock.Hour"/>) it shows in: from FromHour up to ToHour, wrapping past
    /// midnight (22 to 4 is the night). Both or neither.
    /// </summary>
    public int? FromHour { get; set; }
    public int? ToHour { get; set; }

    /// <summary>
    /// A quest, and the state the player's must be in (<see cref="QuestStates"/>). Both or neither.
    /// </summary>
    public int? QuestId { get; set; }
    public string? QuestState { get; set; }

    /// <summary>
    /// An item the player must carry, or with <see cref="ItemAbsent"/> must not.
    /// </summary>
    public int? ItemId { get; set; }
    public bool ItemAbsent { get; set; }

    /// <summary>
    /// Only referees (mods and admins) see it.
    /// </summary>
    public bool RefereesOnly { get; set; }

    [JsonIgnore]
    public bool IsEmpty => FromHour is null && QuestId is null && ItemId is null && !RefereesOnly;

    /// <summary>
    /// Null when the conditions make sense, else what's wrong with them.
    /// </summary>
    public string? Validate()
    {
        if (FromHour is null != ToHour is null)
            return "Give both hours, or neither.";

        if (FromHour is < 0 or > 23 || ToHour is < 0 or > 24)
            return "Hours run from 0 to 24.";

        if (QuestId is null != QuestState is null)
            return "Give a quest and its state, or neither.";

        if (QuestState is not null && Array.IndexOf(QuestStates, QuestState) < 0)
            return $"A quest's state is one of {string.Join(", ", QuestStates)}.";

        if (QuestId <= 0 || ItemId <= 0)
            return "Quest and item ids are above 0.";

        return null;
    }

    /// <summary>
    /// Whether the hour is from <paramref name="from"/> up to <paramref name="to"/>, wrapping past midnight when
    /// <paramref name="to"/> comes first.
    /// </summary>
    public static bool InHours(float hour, int from, int to) => from <= to ? hour >= from && hour < to : hour >= from || hour < to;

    public bool IsMetBy(Player player)
    {
        if (RefereesOnly && !player.IsAdmin && !player.IsMod)
            return false;

        if (FromHour is { } from && ToHour is { } to && !InHours(GameClock.Hour, from, to))
            return false;

        if (QuestId is { } questId)
        {
            var met = QuestState switch
            {
                QuestDone => player.Quests.IsCompleted(questId),
                QuestActive => player.Quests.TryGetInProgress(questId, out _),
                QuestNotDone => !player.Quests.IsCompleted(questId),
                QuestNotStarted => !player.Quests.Contains(questId),
                _ => true
            };

            if (!met)
                return false;
        }

        if (ItemId is { } itemId)
        {
            var carries = false;

            foreach (var item in player.Items)
            {
                if (item.Definition == itemId && item.Count > 0)
                {
                    carries = true;
                    break;
                }
            }

            if (carries == ItemAbsent)
                return false;
        }

        return true;
    }
}
