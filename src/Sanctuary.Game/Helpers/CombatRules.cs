using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

/// <param name="IsKnockedOut">Down and waiting to recover, so a revive can stand them back up.</param>
public readonly record struct ReviveCandidate(ulong Guid, Vector3 Position, bool IsKnockedOut);

public enum ReviveFailure
{
    None,

    /// <summary>
    /// No other player clicked, and no knocked-out player in range.
    /// </summary>
    NoFallenAlly,

    /// <summary>
    /// The clicked player is standing.
    /// </summary>
    NotKnockedOut,

    /// <summary>
    /// The clicked player is knocked out but out of range.
    /// </summary>
    TooFar
}

/// <summary>
/// The numbers behind open-world combat, kept free of entities so they can be tested on their own.
/// </summary>
public static class CombatRules
{
    /// <summary>
    /// Seconds after the last hit given or taken before a player counts as out of combat (and regenerates).
    /// </summary>
    public const int OutOfCombatSeconds = 6;

    public const int PlayerRespawnSeconds = 8;

    /// <summary>
    /// Seconds a revived player has to get clear before mobs notice them again, unless they attack first.
    /// The client's own revive gave players "some time to get clear of the area" too.
    /// </summary>
    public const int ReviveProtectionSeconds = 5;

    /// <summary>
    /// Group members this close to a kill share its stars, coins and loot roll.
    /// </summary>
    public const float GroupShareRadius = 60f;

    /// <summary>
    /// A player's health before buffs: 2500 at rank 1 and 100 more for each rank of the active job.
    /// </summary>
    public static int MaxHitpointsAtRank(int rank) => 2500 + 100 * (Math.Clamp(rank, 1, ProfileExperience.MaxRank) - 1);

    /// <summary>
    /// What a job's abilities hit for at <paramref name="rank"/>: 5% more per rank above 1 (1.95x at the top).
    /// </summary>
    public static int ScaleDamageForRank(int damage, int rank) =>
        damage <= 0 ? damage : (int)Math.Round(damage * (1d + 0.05d * (Math.Clamp(rank, 1, ProfileExperience.MaxRank) - 1)), MidpointRounding.AwayFromZero);

    /// <summary>
    /// A hit of <paramref name="baseDamage"/>, moved by up to +/- <paramref name="variance"/> of itself.
    /// <paramref name="roll"/> is a uniform 0-1 random number. Never less than 1 for a positive base.
    /// </summary>
    public static int RollDamage(int baseDamage, float variance, double roll)
    {
        if (baseDamage <= 0)
            return 0;

        variance = Math.Clamp(variance, 0f, 1f);
        roll = Math.Clamp(roll, 0d, 1d);

        var damage = baseDamage * (1d + variance * (roll * 2d - 1d));

        return Math.Max(1, (int)Math.Round(damage, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Damage left after a DamageReductionPercent stat (e.g. Darkthorne's Embrace). Full reduction blocks it all.
    /// </summary>
    public static int Mitigate(int damage, float reductionPercent)
    {
        if (damage <= 0)
            return 0;

        reductionPercent = Math.Clamp(reductionPercent, 0f, 100f);

        if (reductionPercent >= 100f)
            return 0;

        return Math.Max(1, (int)Math.Round(damage * (1d - reductionPercent / 100d), MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// The combat job to switch a player to when a fight starts: the one they last fought as, else their
    /// highest ranked one. 0 when they own no combat job (one with a toolbar kit in CombatJobs.json).
    /// </summary>
    public static int PickCombatProfileId(IEnumerable<ClientPcProfile> profiles, IReadOnlyCollection<int> combatJobIds, int lastCombatProfileId)
    {
        var owned = profiles.Where(profile => combatJobIds.Contains(profile.Id)).ToList();

        if (lastCombatProfileId != 0 && owned.Any(profile => profile.Id == lastCombatProfileId))
            return lastCombatProfileId;

        return owned
            .OrderByDescending(profile => profile.Rank)
            .Select(profile => profile.Id)
            .FirstOrDefault();
    }

    /// <summary>
    /// The health a revive stands a player back up with: <paramref name="percent"/> of their maximum, at least 1.
    /// </summary>
    public static int ReviveHitpoints(int maxHitpoints, float percent)
    {
        if (maxHitpoints <= 0)
            return 1;

        percent = float.IsFinite(percent) ? Math.Clamp(percent, 0f, 100f) : 0f;

        return Math.Clamp((int)Math.Round(maxHitpoints * percent / 100d, MidpointRounding.AwayFromZero), 1, maxHitpoints);
    }

    /// <summary>
    /// Who a revive (the Resurrection Scroll) stands back up: the clicked player, who has to be knocked out and
    /// within <paramref name="range"/>, or with no other player clicked, the nearest knocked-out player in range.
    /// The user never revives themself.
    /// </summary>
    public static ReviveFailure PickReviveTarget(ulong userGuid, Vector3 userPosition, ulong clickedGuid,
        IReadOnlyList<ReviveCandidate> players, float range, out ulong targetGuid)
    {
        targetGuid = 0;

        var rangeSquared = range * range;

        // A clicked player is the one meant, so a standing or far-off one isn't swapped for someone else.
        foreach (var player in players)
        {
            if (clickedGuid == 0 || player.Guid != clickedGuid || player.Guid == userGuid)
                continue;

            if (!player.IsKnockedOut)
                return ReviveFailure.NotKnockedOut;

            if (Vector3.DistanceSquared(userPosition, player.Position) > rangeSquared)
                return ReviveFailure.TooFar;

            targetGuid = player.Guid;

            return ReviveFailure.None;
        }

        var nearestDistance = float.MaxValue;

        foreach (var player in players)
        {
            if (player.Guid == userGuid || !player.IsKnockedOut)
                continue;

            var distance = Vector3.DistanceSquared(userPosition, player.Position);

            if (distance > rangeSquared || distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            targetGuid = player.Guid;
        }

        return targetGuid == 0 ? ReviveFailure.NoFallenAlly : ReviveFailure.None;
    }
}
