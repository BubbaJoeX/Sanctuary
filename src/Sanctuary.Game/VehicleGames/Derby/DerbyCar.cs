using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Packet;

namespace Sanctuary.Game.VehicleGames.Derby;

/// <summary>
/// A car in the derby: a player's (whose client drives it and streams its state) or an AI car the server drives. Its
/// health, score and tallies are the server's alone.
/// </summary>
public sealed class DerbyCar
{
    public DerbyCar(ulong guid, VehicleConfig config, Vector3 spawnPosition, float spawnHeading)
    {
        Guid = guid;
        Config = config;
        SpawnPosition = spawnPosition;
        SpawnHeading = spawnHeading;

        Position = spawnPosition;
        Heading = spawnHeading;
    }

    public ulong Guid { get; }

    public VehicleConfig Config { get; }

    /// <summary>
    /// The player driving it, or null for an AI car.
    /// </summary>
    public ArenaParticipant? Participant { get; init; }

    public bool IsPlayer => Participant is not null;

    /// <summary>
    /// An AI car's driver, an NPC with the car's guid.
    /// </summary>
    public Npc? Driver { get; set; }

    public Vector3 SpawnPosition { get; }
    public float SpawnHeading { get; }

    #region Motion

    public Vector3 Position { get; set; }

    /// <summary>
    /// Which way it faces (forward is (sin, 0, cos)).
    /// </summary>
    public float Heading { get; set; }

    public Vector3 Velocity { get; set; }

    /// <summary>
    /// An AI car's speed along its heading (negative backing up).
    /// </summary>
    public float Speed { get; set; }

    public float YawRate { get; set; }

    /// <summary>
    /// An AI car's push from a crash, fading.
    /// </summary>
    public Vector3 Knock { get; set; }

    public Vector3 Up { get; set; } = Vector3.UnitY;

    public float Throttle { get; set; }
    public float Brake { get; set; }
    public float Steering { get; set; }

    /// <summary>
    /// A player's car's last state from its client, as it sent it (the client's clock orders them).
    /// </summary>
    public VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyData? LastState { get; set; }

    #endregion

    #region The match

    public int Health { get; set; } = DerbyRules.MaxHealth;
    public int Score { get; set; }
    public int Wrecks { get; set; }
    public int TimesWrecked { get; set; }
    public int DamageDealt { get; set; }
    public int DamageTaken { get; set; }

    /// <summary>
    /// Body parts still on (they come off as it takes damage).
    /// </summary>
    public bool[] Parts { get; } = [true, true, true];

    /// <summary>
    /// Each corner's dents (front left, front right, back left, back right).
    /// </summary>
    public float[] Dents { get; } = new float[4];

    public bool IsWrecked => Health <= 0;

    /// <summary>
    /// When it was wrecked (and goes back to its spawn), 0 while it's running.
    /// </summary>
    public long WreckedAt { get; set; }

    /// <summary>
    /// Whoever hit it last, for the wreck's credit.
    /// </summary>
    public ulong LastAttacker { get; set; }
    public long LastHitAt { get; set; }

    public long ShieldUntil { get; set; }
    public long AttackUpUntil { get; set; }
    public long UltimateUntil { get; set; }

    /// <summary>
    /// The state data changed since it was last sent.
    /// </summary>
    public bool Dirty { get; set; } = true;
    public long StateSentAt { get; set; }

    #endregion

    #region AI

    /// <summary>
    /// The car it's going for, or 0 while it roams (to <see cref="RoamTo"/>: a box, or anywhere).
    /// </summary>
    public ulong TargetGuid { get; set; }
    public Vector3 RoamTo { get; set; }
    public long RetargetAt { get; set; }
    public long ReverseUntil { get; set; }
    public float ReverseSteering { get; set; }
    public long StuckSince { get; set; }

    /// <summary>
    /// How fast this driver goes, as a share of the top speed.
    /// </summary>
    public float Pace { get; set; } = 1f;

    public long PositionSentAt { get; set; }

    #endregion

    public Vector3 Forward => ArenaRules.Forward(Heading);

    public VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyStateData GetStateData() => new()
    {
        Guid = Guid,
        Health = Health,
        Score = Score,
        Wrecks = Wrecks,
        TimesWrecked = TimesWrecked,
        DamageDealt = DamageDealt,
        DamageTaken = DamageTaken,
        Part = Parts[0] ? (byte)1 : (byte)0,
        Part2 = Parts[1] ? (byte)1 : (byte)0,
        Part3 = Parts[2] ? (byte)1 : (byte)0,
        Dent = Dents[0],
        Dent2 = Dents[1],
        Dent3 = Dents[2],
        Dent4 = Dents[3]
    };

    /// <summary>
    /// Back at its spawn, repaired.
    /// </summary>
    public void Reset()
    {
        Health = DerbyRules.MaxHealth;
        WreckedAt = 0;
        LastAttacker = 0;

        Parts[0] = Parts[1] = Parts[2] = true;

        for (var i = 0; i < Dents.Length; i++)
            Dents[i] = 0f;

        Position = SpawnPosition;
        Heading = SpawnHeading;
        Velocity = Vector3.Zero;
        Knock = Vector3.Zero;
        Speed = 0f;
        YawRate = 0f;
        Up = Vector3.UnitY;

        Dirty = true;
    }
}
