using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Achievements;

/// <summary>
/// Achievements (Achievements.json): counts the game events their objectives name, tells the client as they start,
/// move and finish, saves them, and gives their rewards. The client computes nothing itself.
/// </summary>
public class AchievementManager : IAchievementManager
{
    private readonly ILogger _logger;
    private readonly Func<AchievementDefinitionCollection> _definitions;
    private readonly IRewardManager? _rewardManager;
    private readonly IDbContextFactory<DatabaseContext>? _dbContextFactory;

    public AchievementManager(ILogger<AchievementManager> logger, IResourceManager resourceManager, IRewardManager rewardManager,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _logger = logger;
        _definitions = () => resourceManager.Achievements;
        _rewardManager = rewardManager;
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Counting only, for tests and tools: nothing is saved and no reward is given.
    /// </summary>
    public AchievementManager(ILogger logger, AchievementDefinitionCollection definitions)
    {
        _logger = logger;
        _definitions = () => definitions;
    }

    private AchievementDefinitionCollection Definitions => _definitions();

    public void Load(Player player, IEnumerable<DbCharacterAchievement> saved)
    {
        var achievements = player.Achievements;

        lock (achievements)
        {
            achievements.Started.Clear();
            achievements.PendingRewards.Clear();
            achievements.ClientHasList = false;

            foreach (var row in saved)
            {
                var progress = new AchievementProgress
                {
                    AchievementId = row.AchievementId,
                    Started = row.Started,
                    Finished = row.Finished
                };

                foreach (var objective in row.Objectives)
                    progress.Objectives[objective.ObjectiveId] = new ObjectiveProgress { Count = objective.Count, Completed = objective.Completed };

                achievements.Started[row.AchievementId] = progress;
            }

            achievements.Manager = this;
            achievements.Player = player;
        }

        // What the player already has counts, so a character made before achievements existed gets them now.
        foreach (var profile in player.Profiles)
            Record(player, AchievementEvent.JobRank, profile.Id, profile.Rank);

        if (player.Pets.Count > 0)
            Record(player, AchievementEvent.PetAcquired, 0, 1);

        foreach (var mount in player.Mounts.Select(mount => mount.Definition).Distinct())
            Record(player, AchievementEvent.MountAcquired, mount, 1);
    }

    public List<ClientAchievement> GetStarted(Player player, out int points)
    {
        var achievements = player.Achievements;
        var list = new List<ClientAchievement>();

        points = 0;

        lock (achievements)
        {
            foreach (var progress in achievements.Started.Values.OrderBy(progress => progress.AchievementId))
            {
                if (!Definitions.TryGetValue(progress.AchievementId, out var definition))
                    continue;

                list.Add(Build(definition, progress));

                if (progress.Finished is not null)
                    points += Definitions.PointsOf(definition);
            }
        }

        return list;
    }

    public void SendList(Player player)
    {
        var achievements = player.Achievements;
        var packet = new AchievementInitializePacket();
        List<int> rewards;

        lock (achievements)
        {
            foreach (var definition in Definitions.Values.OrderBy(definition => definition.Id))
            {
                achievements.Started.TryGetValue(definition.Id, out var progress);
                packet.Achievements.Add(Build(definition, progress));
            }

            achievements.ClientHasList = true;

            rewards = [.. achievements.PendingRewards];
            achievements.PendingRewards.Clear();
        }

        player.SendTunneled(packet);

        foreach (var achievementId in rewards)
            GiveReward(player, achievementId);
    }

    public void Record(Player player, AchievementEvent achievementEvent, int key, int count)
    {
        if (achievementEvent == AchievementEvent.None || count <= 0 && !IsTotal(achievementEvent))
            return;

        Count(player, achievementEvent, count,
            objective => objective.Trigger!.Keys is not { } keys || keys.Contains(key));
    }

    public void RecordKill(Player player, Npc mob)
    {
        Count(player, AchievementEvent.Kill, 1,
            objective => Definitions.CountsKill(objective, mob.Name, mob.NameId));
    }

    /// <summary>
    /// A job's rank, a house's items and the quests done are totals the event reports outright; everything else
    /// adds up.
    /// </summary>
    private static bool IsTotal(AchievementEvent achievementEvent) =>
        achievementEvent is AchievementEvent.JobRank or AchievementEvent.HouseItems or AchievementEvent.QuestCount;

    private void Count(Player player, AchievementEvent achievementEvent, int count, Func<AchievementObjectiveDefinition, bool> matches)
    {
        var achievements = player.Achievements;
        var finished = new List<AchievementDefinition>();

        lock (achievements)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var changed = new Dictionary<int, (AchievementProgress Progress, bool IsNew, List<int> Objectives)>();

            foreach (var (definition, objective) in Definitions.CountedBy(achievementEvent))
            {
                if (!matches(objective))
                    continue;

                achievements.Started.TryGetValue(definition.Id, out var progress);

                if (progress?.Finished is not null)
                    continue;

                var objectiveProgress = progress?.Objectives.GetValueOrDefault(objective.Id);

                if (objectiveProgress?.Completed == true)
                    continue;

                var oldCount = objectiveProgress?.Count ?? 0;
                var newCount = IsTotal(achievementEvent) ? Math.Max(oldCount, count) : oldCount + count;

                if (newCount == oldCount)
                    continue;

                var isNew = progress is null;

                if (progress is null)
                {
                    progress = new AchievementProgress { AchievementId = definition.Id, Started = now };

                    foreach (var other in definition.Objectives)
                        progress.Objectives[other.Id] = new ObjectiveProgress();

                    achievements.Started[definition.Id] = progress;
                }

                objectiveProgress = progress.Objectives.TryGetValue(objective.Id, out var existing)
                    ? existing
                    : progress.Objectives[objective.Id] = new ObjectiveProgress();

                objectiveProgress.Count = Math.Min(newCount, objective.Required);
                objectiveProgress.Completed = newCount >= objective.Required;

                if (!changed.TryGetValue(definition.Id, out var change))
                    changed[definition.Id] = change = (progress, isNew, []);

                change.Objectives.Add(objective.Id);
            }

            foreach (var (achievementId, (progress, isNew, objectiveIds)) in changed)
            {
                var definition = Definitions[achievementId];

                if (definition.Objectives.All(objective => progress.Objectives.GetValueOrDefault(objective.Id)?.Completed == true))
                {
                    progress.Finished = now;
                    finished.Add(definition);
                }

                if (achievements.ClientHasList)
                    SendProgress(player, definition, progress, isNew, objectiveIds);

                Save(player, progress, objectiveIds);
            }
        }

        foreach (var definition in finished)
        {
            _logger.LogInformation("{player} earned achievement {id} ({comment}).", player.Name.FullName, definition.Id, definition.Comment);

            lock (achievements)
            {
                if (!achievements.ClientHasList)
                {
                    achievements.PendingRewards.Add(definition.Id);
                    continue;
                }
            }

            player.SendTunneled(new AchievementCompletePacket { AchievementId = definition.Id });

            if (Definitions.GainEffectOf(definition) is > 0 and var effect)
            {
                player.SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect
                {
                    Guid = player.Guid,
                    CompositeEffectId = effect,
                    EffectDelay = 1000,
                    Position = player.Position
                }, true);
            }

            GiveReward(player, definition.Id);
        }

        // The "do all of these" achievements count the ones just finished.
        foreach (var definition in finished)
            Record(player, AchievementEvent.Achievement, definition.Id, 1);
    }

    private void SendProgress(Player player, AchievementDefinition definition, AchievementProgress progress, bool isNew,
        List<int> changedObjectiveIds)
    {
        var bar = ProgressOf(definition, progress);

        // A newly started achievement gets all its objectives tracked; after that only the ones that moved change.
        var objectives = isNew ? definition.Objectives : definition.Objectives.Where(objective => changedObjectiveIds.Contains(objective.Id));

        foreach (var objective in objectives)
        {
            var objectiveProgress = progress.Objectives.GetValueOrDefault(objective.Id) ?? new ObjectiveProgress();

            if (isNew)
            {
                player.SendTunneled(new AchievementObjectiveActivatedPacket
                {
                    AchievementId = definition.Id,
                    ObjectiveId = objective.Id,
                    CurrentCount = objectiveProgress.Count,
                    RequiredCount = objective.Required,
                    Progress = bar
                });
            }
            else
            {
                player.SendTunneled(new AchievementObjectiveUpdatePacket
                {
                    AchievementId = definition.Id,
                    ObjectiveId = objective.Id,
                    CurrentCount = objectiveProgress.Count,
                    Progress = bar
                });
            }

            if (objectiveProgress.Completed && changedObjectiveIds.Contains(objective.Id))
            {
                player.SendTunneled(new AchievementObjectiveCompletePacket
                {
                    AchievementId = definition.Id,
                    ObjectiveId = objective.Id,
                    Progress = bar
                });
            }
        }
    }

    private void GiveReward(Player player, int achievementId)
    {
        if (!Definitions.TryGetValue(achievementId, out var definition) || definition.Reward is not { } reward || _rewardManager is null)
            return;

        if (!_rewardManager.TryGrantItem(player, reward.ItemId, 0, reward.Quantity))
            _logger.LogWarning("Failed to give {player} achievement {id}'s reward, item {item}.", player.Name.FullName, achievementId, reward.ItemId);
    }

    private void Save(Player player, AchievementProgress progress, List<int> objectiveIds)
    {
        if (_dbContextFactory is null)
            return;

        try
        {
            using var db = _dbContextFactory.CreateDbContext();

            var row = db.CharacterAchievements
                .Include(achievement => achievement.Objectives)
                .SingleOrDefault(achievement => achievement.CharacterId == player.CharacterId && achievement.AchievementId == progress.AchievementId);

            if (row is null)
            {
                row = new DbCharacterAchievement
                {
                    CharacterId = player.CharacterId,
                    AchievementId = progress.AchievementId,
                    Started = progress.Started
                };

                db.CharacterAchievements.Add(row);
            }

            row.Finished = progress.Finished;

            foreach (var objectiveId in objectiveIds)
            {
                if (!progress.Objectives.TryGetValue(objectiveId, out var objectiveProgress))
                    continue;

                var objectiveRow = row.Objectives.SingleOrDefault(objective => objective.ObjectiveId == objectiveId);

                if (objectiveRow is null)
                {
                    objectiveRow = new DbCharacterAchievementObjective
                    {
                        CharacterId = player.CharacterId,
                        AchievementId = progress.AchievementId,
                        ObjectiveId = objectiveId
                    };

                    row.Objectives.Add(objectiveRow);
                }

                objectiveRow.Count = objectiveProgress.Count;
                objectiveRow.Completed = objectiveProgress.Completed;
            }

            db.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save achievement {id} for {player}.", progress.AchievementId, player.Name.FullName);
        }
    }

    /// <summary>
    /// The bar: each objective's share, done ones full.
    /// </summary>
    private static float ProgressOf(AchievementDefinition definition, AchievementProgress? progress)
    {
        if (progress?.Finished is not null)
            return 1f;

        if (progress is null || definition.Objectives.Count == 0)
            return 0f;

        var sum = 0f;

        foreach (var objective in definition.Objectives)
        {
            var objectiveProgress = progress.Objectives.GetValueOrDefault(objective.Id);

            sum += objectiveProgress is null ? 0f
                : objectiveProgress.Completed ? 1f
                : Math.Min(1f, objectiveProgress.Count / (float)objective.Required);
        }

        return sum / definition.Objectives.Count;
    }

    /// <summary>
    /// The window's row for an achievement and the player's progress with it (none = not started).
    /// </summary>
    private ClientAchievement Build(AchievementDefinition definition, AchievementProgress? progress)
    {
        var achievement = new ClientAchievement
        {
            Id = definition.Id,
            NameId = definition.NameId,
            DescriptionId = definition.DescriptionId,
            MembersOnly = definition.MembersOnly,
            TimeStarted = progress is null ? 0 : (ulong)progress.Started,
            TimeFinished = progress?.Finished is { } finished ? (ulong)finished : 0,
            Progress = ProgressOf(definition, progress),
            IconId = definition.IconId,
            IconTint = definition.IconTint,
            TrophyId = definition.TrophyId,
            BonusPoints = definition.BonusPoints,
            CategoryId = definition.CategoryId
        };

        if (definition.Reward is { } reward)
        {
            achievement.Reward = new RewardBundleBase
            {
                Success = false,
                Unknown3 = 15,
                Multiplier = 1.0f,
                IconId = reward.IconId,
                NameId = reward.NameId,
                DescriptionId = reward.DescriptionId
            };

            achievement.Reward.Entries.Add(new RewardBundleEntryItem
            {
                IconId = reward.IconId,
                TintId = reward.IconTint,
                NameId = reward.NameId,
                Quantity = reward.Quantity,
                DefinitionId = reward.ItemId,
                ItemTextColor = reward.TextColor
            });
        }

        foreach (var objective in definition.Objectives)
        {
            var objectiveProgress = progress?.Objectives.GetValueOrDefault(objective.Id);
            var done = progress?.Finished is not null || objectiveProgress?.Completed == true;

            achievement.Objectives.Add(new QuestObjectiveInfo
            {
                ObjectiveId = objective.Id,
                NameId = objective.NameId,
                DescriptionId = objective.DescriptionId,
                RewardBundle = ClientAchievement.EmptyReward(),
                State = done ? 2 : progress is null ? 0 : 1,
                CurrentCount = done ? objective.Required : objectiveProgress?.Count ?? 0,
                RequiredCount = objective.Required
            });
        }

        return achievement;
    }
}
