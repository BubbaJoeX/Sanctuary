using System.Collections.Generic;

using Sanctuary.Game.Entities;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Achievements;

/// <summary>
/// A player's achievement progress, and where game code reports the events that count toward it. Until the player
/// has logged in (no manager yet), reporting does nothing.
/// </summary>
public sealed class PlayerAchievements
{
    /// <summary>
    /// Started achievements by id. Locked by the manager while it reads or changes them.
    /// </summary>
    internal Dictionary<int, AchievementProgress> Started { get; } = [];

    /// <summary>
    /// Rewards earned before the client could be told (catching up at login), given once it can.
    /// </summary>
    internal List<int> PendingRewards { get; } = [];

    internal IAchievementManager? Manager { get; set; }

    internal Player? Player { get; set; }

    /// <summary>
    /// Whether the client has the full list (AchievementInitialize): before that it ignores progress packets.
    /// </summary>
    internal bool ClientHasList { get; set; }

    public bool IsStarted(int achievementId)
    {
        lock (this)
            return Started.ContainsKey(achievementId);
    }

    public bool IsFinished(int achievementId)
    {
        lock (this)
            return Started.TryGetValue(achievementId, out var progress) && progress.Finished is not null;
    }

    /// <summary>
    /// An objective's count (0 if the achievement isn't started).
    /// </summary>
    public int CountOf(int achievementId, int objectiveId)
    {
        lock (this)
            return Started.TryGetValue(achievementId, out var progress) && progress.Objectives.TryGetValue(objectiveId, out var objective)
                ? objective.Count
                : 0;
    }

    /// <summary>
    /// Counts an event toward every objective it matches: <paramref name="count"/> is added, or for a job's rank
    /// and a house's items, is the new total.
    /// </summary>
    public void Record(AchievementEvent achievementEvent, int key = 0, int count = 1)
    {
        if (Manager is not null && Player is not null)
            Manager.Record(Player, achievementEvent, key, count);
    }

    /// <summary>
    /// Every achievement with this player's progress, which the client needs before progress packets mean anything.
    /// </summary>
    public void SendList()
    {
        if (Manager is not null && Player is not null)
            Manager.SendList(Player);
    }

    /// <summary>
    /// The started and finished achievements, and the points they're worth (none before login).
    /// </summary>
    public List<ClientAchievement> GetStarted(out int points)
    {
        points = 0;

        return Manager is not null && Player is not null ? Manager.GetStarted(Player, out points) : [];
    }

    /// <summary>
    /// A mob the player helped defeat.
    /// </summary>
    public void RecordKill(Npc mob)
    {
        if (Manager is not null && Player is not null)
            Manager.RecordKill(Player, mob);
    }
}

internal sealed class AchievementProgress
{
    public required int AchievementId { get; init; }

    /// <summary>
    /// Unix seconds.
    /// </summary>
    public long Started { get; set; }

    /// <summary>
    /// Unix seconds; null while it isn't done.
    /// </summary>
    public long? Finished { get; set; }

    public Dictionary<int, ObjectiveProgress> Objectives { get; } = [];
}

internal sealed class ObjectiveProgress
{
    public int Count { get; set; }

    public bool Completed { get; set; }
}
