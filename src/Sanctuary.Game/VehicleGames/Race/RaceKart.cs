using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Packet;

namespace Sanctuary.Game.VehicleGames.Race;

/// <summary>
/// A kart in the race: a player's (whose client drives it and streams its state) or an AI kart the server drives
/// along its driver's line. Its progress round the race is the server's alone.
/// </summary>
public sealed class RaceKart
{
    public RaceKart(ulong guid, int slot, VehicleConfig config, Vector3 spawnPosition, float spawnHeading)
    {
        Guid = guid;
        Slot = slot;
        Config = config;
        SpawnPosition = spawnPosition;
        SpawnHeading = spawnHeading;

        Position = spawnPosition;
        Heading = spawnHeading;
    }

    public ulong Guid { get; }

    /// <summary>
    /// Its grid slot (0-7), which kart state packets address it by.
    /// </summary>
    public int Slot { get; }

    public VehicleConfig Config { get; }

    /// <summary>
    /// The player driving it, or null for an AI kart.
    /// </summary>
    public ArenaParticipant? Participant { get; init; }

    public bool IsPlayer => Participant is not null;

    /// <summary>
    /// An AI kart's driver, an NPC with the kart's guid.
    /// </summary>
    public Npc? Driver { get; set; }

    public Vector3 SpawnPosition { get; }
    public float SpawnHeading { get; }

    public RaceProgress Progress { get; } = new();

    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public float Heading { get; set; }

    /// <summary>
    /// A player's kart's last state from its client (its clock orders them).
    /// </summary>
    public VehicleRacePacketUpdateVehicleRaceServerData? LastState { get; set; }

    /// <summary>
    /// The client's clock when it registered, which its kart's spawn state carries.
    /// </summary>
    public ulong RegisterClock { get; set; }

    #region AI

    public DriverLine? Line { get; set; }

    /// <summary>
    /// Still on the start lane (<see cref="DriverLine.Start"/>), or on the lap line.
    /// </summary>
    public bool OnStartLane { get; set; } = true;

    public float LineDistance { get; set; }

    public float Speed { get; set; }

    /// <summary>
    /// How fast this driver goes, as a share of the live line's speed.
    /// </summary>
    public float Pace { get; set; } = 1f;

    /// <summary>
    /// Sideways off its line (to get past another kart), easing to <see cref="TargetOffset"/>.
    /// </summary>
    public float Offset { get; set; }
    public float TargetOffset { get; set; }

    public long SlowedUntil { get; set; }
    public long BoostedUntil { get; set; }

    public long PositionSentAt { get; set; }

    #endregion

    #region A player's items

    /// <summary>
    /// The items held, front first (at most three).
    /// </summary>
    public Queue<RaceItem> Items { get; } = new();

    /// <summary>
    /// When a used timed item's effect ends.
    /// </summary>
    public Dictionary<RaceItem, long> Effects { get; } = [];

    public long LastBumpAt { get; set; }

    /// <summary>
    /// When it was found off the track (fallen), 0 while it's on it.
    /// </summary>
    public long OffTrackSince { get; set; }

    #endregion
}

/// <summary>
/// A kart race item (PickupHitResponse's, UseItemResponse's).
/// </summary>
public enum RaceItem
{
    Missile = 1,
    TimedEffect = 2,
    Mine = 3,
    Shield = 4,
    Boost = 5
}
