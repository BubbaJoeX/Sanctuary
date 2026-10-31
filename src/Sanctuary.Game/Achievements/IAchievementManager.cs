using System.Collections.Generic;

using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Achievements;

public interface IAchievementManager
{
    /// <summary>
    /// Takes a logging-in player's saved progress, then quietly credits what they already have (job ranks, pets,
    /// rides) so the lists they get at login are right.
    /// </summary>
    void Load(Player player, IEnumerable<DbCharacterAchievement> saved);

    /// <summary>
    /// The started and finished achievements, and their points, for SendSelf.
    /// </summary>
    List<ClientAchievement> GetStarted(Player player, out int points);

    /// <summary>
    /// Every achievement with the player's progress (AchievementInitialize), on each zone load. Gives any reward
    /// earned while catching up.
    /// </summary>
    void SendList(Player player);

    void Record(Player player, AchievementEvent achievementEvent, int key, int count);

    void RecordKill(Player player, Npc mob);
}
