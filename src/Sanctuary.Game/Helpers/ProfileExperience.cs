using System;

namespace Sanctuary.Game.Helpers;

/// <summary>
/// A job's rank and the experience (stars) it has toward the next one. DbProfile keeps the same pair
/// (Level, LevelXP).
/// </summary>
public readonly record struct ProfileProgress(int Rank, int Experience);

/// <summary>
/// How jobs level up: stars from kills, quests and collections fill the active job's bar, and each rank
/// asks for a little more than the last. Kept free of entities so it can be tested on its own.
/// </summary>
public static class ProfileExperience
{
    public const int MaxRank = 20;

    /// <summary>
    /// The PFX_levelup_big composite effect, played on a player who ranks up.
    /// </summary>
    public const int LevelUpEffectId = 15117;

    /// <summary>
    /// Stars needed to go from <paramref name="rank"/> to the next rank: 100 at rank 1, then 75 more per
    /// rank (1450 at rank 19). A default mob is worth 25 and a quest about 85. 0 at the top rank.
    /// </summary>
    public static int ExperienceToNextRank(int rank)
    {
        if (rank >= MaxRank)
            return 0;

        return 100 + 75 * (Math.Max(rank, 1) - 1);
    }

    /// <summary>
    /// <paramref name="current"/> after earning <paramref name="experience"/> stars, carrying the leftover
    /// into each new rank. The top rank keeps no experience.
    /// </summary>
    public static ProfileProgress Add(ProfileProgress current, int experience)
    {
        var rank = Math.Clamp(current.Rank, 1, MaxRank);
        var total = (long)Math.Max(current.Experience, 0) + Math.Max(experience, 0);

        while (rank < MaxRank && total >= ExperienceToNextRank(rank))
        {
            total -= ExperienceToNextRank(rank);
            rank++;
        }

        return new ProfileProgress(rank, rank >= MaxRank ? 0 : (int)total);
    }

    /// <summary>
    /// How full the job's bar is, 0-99 (ClientPcProfile.RankPercent); 100 at the top rank.
    /// </summary>
    public static int RankPercent(ProfileProgress progress)
    {
        var needed = ExperienceToNextRank(progress.Rank);

        if (needed <= 0)
            return 100;

        return Math.Clamp(progress.Experience * 100 / needed, 0, 99);
    }
}
