using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Sanctuary.Game.VehicleGames.Derby;

/// <summary>
/// The derby's rules, read off the live match: a crash takes 10, 20 or 42 health by how hard it was (never more than
/// the car has left), dealt by the car that drove into the other, or by both when they met head on. A wrecked car is
/// worth 2 to whoever wrecked it (3 for the leader) and costs its own driver 1 as it comes back, about four seconds
/// later, repaired.
/// </summary>
public static class DerbyRules
{
    public const int MaxHealth = 100;

    /// <summary>
    /// How high a car's origin rides over the floor (live AI cars were 0.115 to 0.119 m over the server's terrain).
    /// </summary>
    public const float RideHeight = 0.117f;

    /// <summary>
    /// Closing speeds (m/s) below this are a nudge, not a crash.
    /// </summary>
    public const float MinCrashSpeed = 3f;

    /// <summary>
    /// When one car's speed into the other beats the other's by this much (m/s), it alone is the attacker.
    /// </summary>
    public const float AttackerMargin = 2f;

    public const int WreckPoints = 2;
    public const int LeaderWreckPoints = 3;
    public const int WreckedPenalty = 1;

    /// <summary>
    /// A wrecked car goes back to its spawn this long after (live 3.5 to 4 s).
    /// </summary>
    public const int ResetDelayMs = 4000;

    /// <summary>
    /// Two cars can't crash again sooner than this.
    /// </summary>
    public const int CrashCooldownMs = 1500;

    public const float ShieldSeconds = 10f;
    public const float AttackUpSeconds = 10f;
    public const float UltimateSeconds = 8f;
    public const int HealthPickup = 50;

    /// <summary>
    /// A crash's damage by the attacker's speed into the victim: live hits took 10, 20 or 42.
    /// </summary>
    public static int CrashDamage(float speed) => speed switch
    {
        < MinCrashSpeed => 0,
        < 9f => 10,
        < 16f => 20,
        _ => 42
    };

    /// <summary>
    /// Attack up makes a crash half again as bad, the ultimate twice.
    /// </summary>
    public static int Boost(int damage, bool attackUp, bool ultimate) =>
        ultimate ? damage * 2 : attackUp ? (int)MathF.Round(damage * 1.5f) : damage;

    /// <summary>
    /// Who hit whom in a crash between <paramref name="a"/> and <paramref name="b"/>, and how hard: each car's speed
    /// into the other along the line between them. Null if they weren't closing fast enough to count.
    /// </summary>
    public static (float SpeedA, float SpeedB)? Closing(Vector3 positionA, Vector3 velocityA, Vector3 positionB, Vector3 velocityB)
    {
        var between = positionB - positionA;
        between.Y = 0f;

        if (between.LengthSquared() < 1e-4f)
            return null;

        var normal = Vector3.Normalize(between);

        var speedA = Vector3.Dot(velocityA, normal);
        var speedB = -Vector3.Dot(velocityB, normal);

        if (speedA + speedB < MinCrashSpeed)
            return null;

        return (speedA, speedB);
    }

    /// <summary>
    /// The damage each car deals the other in a crash: the faster one into the other alone, or both when it's close.
    /// </summary>
    public static (int ByA, int ByB) CrashDamage(float speedA, float speedB)
    {
        if (speedA - speedB >= AttackerMargin)
            return (CrashDamage(speedA + Math.Max(0f, speedB)), 0);

        if (speedB - speedA >= AttackerMargin)
            return (0, CrashDamage(speedB + Math.Max(0f, speedA)));

        // Head on (or side by side): each deals what its own speed earns, at least a light knock.
        var both = Math.Max(10, CrashDamage(speedA + speedB) / 2);

        return (both, both);
    }

    /// <summary>
    /// <paramref name="attacker"/> takes <paramref name="damage"/> health off <paramref name="victim"/> (at most what
    /// it has, none through a shield). Returns what it took.
    /// </summary>
    public static int Hit(DerbyCar attacker, DerbyCar victim, int damage, long now)
    {
        if (damage <= 0 || victim.IsWrecked || attacker.IsWrecked || victim.ShieldUntil > now)
            return 0;

        damage = Math.Min(Boost(damage, attacker.AttackUpUntil > now, attacker.UltimateUntil > now), victim.Health);

        victim.Health -= damage;
        victim.DamageTaken += damage;
        victim.LastAttacker = attacker.Guid;
        victim.LastHitAt = now;
        attacker.DamageDealt += damage;

        Dent(victim, attacker.Position, damage);

        attacker.Dirty = true;
        victim.Dirty = true;

        return damage;
    }

    /// <summary>
    /// The corner nearest the hit dents, and parts come off as the car's health drops past 80, 60 and 40 (a live
    /// car on 38 had none left).
    /// </summary>
    private static void Dent(DerbyCar car, Vector3 from, int damage)
    {
        var local = from - car.Position;
        var forward = car.Forward;
        var right = new Vector3(forward.Z, 0f, -forward.X);

        var front = Vector3.Dot(local, forward) >= 0f;
        var rightSide = Vector3.Dot(local, right) >= 0f;
        var corner = (front ? 0 : 2) + (rightSide ? 1 : 0);

        car.Dents[corner] = Math.Min(0.17f, car.Dents[corner] + damage * 0.0025f);

        for (var i = 0; i < car.Parts.Length; i++)
            car.Parts[i] = car.Health > 80 - 20 * i;
    }

    /// <summary>
    /// <paramref name="victim"/> was wrecked: its attacker scores (more for wrecking the leader).
    /// </summary>
    public static void Wreck(DerbyCar? attacker, DerbyCar victim, bool victimWasLeader, long now)
    {
        victim.WreckedAt = now;
        victim.Speed = 0f;
        victim.Dirty = true;

        if (attacker is null)
            return;

        attacker.Wrecks++;
        attacker.Score += victimWasLeader ? LeaderWreckPoints : WreckPoints;
        attacker.Dirty = true;
    }

    /// <summary>
    /// A wrecked car comes back: one more time wrecked, a point off (never below 0), repaired at its spawn.
    /// </summary>
    public static void Respawn(DerbyCar car)
    {
        car.TimesWrecked++;
        car.Score = Math.Max(0, car.Score - WreckedPenalty);

        car.Reset();
    }

    /// <summary>
    /// The single car with the best score, or null while nobody has scored or two share it.
    /// </summary>
    public static DerbyCar? Leader(IEnumerable<DerbyCar> cars)
    {
        DerbyCar? leader = null;
        var tied = false;

        foreach (var car in cars)
        {
            if (car.Score <= 0)
                continue;

            if (leader is null || car.Score > leader.Score)
            {
                leader = car;
                tied = false;
            }
            else if (car.Score == leader.Score)
            {
                tied = true;
            }
        }

        return tied ? null : leader;
    }

    /// <summary>
    /// The final order: score, then damage dealt less taken (the results' Damage column), then wrecks.
    /// </summary>
    public static List<DerbyCar> Standings(IEnumerable<DerbyCar> cars) =>
        cars.OrderByDescending(car => car.Score)
            .ThenByDescending(car => car.DamageDealt - car.DamageTaken)
            .ThenByDescending(car => car.Wrecks)
            .ToList();
}
