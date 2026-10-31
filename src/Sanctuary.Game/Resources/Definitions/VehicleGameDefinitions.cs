using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Sanctuary.Game.Resources.Definitions;

/// <summary>
/// The 3D games (see VehicleGames.json): kart racing, the demolition derby and soccer, which the live game ran as
/// encounters in their own zones against server-driven AI, and the kart garage.
/// </summary>
public sealed class VehicleGamesDefinition
{
    public List<VehicleGameStageDefinition> Stages { get; set; } = [];

    public DerbyArenaDefinition Derby { get; set; } = new();

    public List<RaceTrackDefinition> Tracks { get; set; } = [];

    public SoccerPitchDefinition Soccer { get; set; } = new();
}

/// <summary>
/// The encounter's kind (the client's EncounterDetails kind), which picks the game.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VehicleGameKind>))]
public enum VehicleGameKind
{
    Race = 2,
    Derby = 3,
    Soccer = 4,
    Garage = 12
}

/// <summary>
/// One game a player can be offered: a MiniGameData.txt row (its id is the stage definition id the client gets),
/// its zone and the job it belongs to.
/// </summary>
public sealed class VehicleGameStageDefinition
{
    public int Id { get; set; }

    public string Comment { get; set; } = string.Empty;

    public VehicleGameKind Kind { get; set; }

    /// <summary>
    /// MiniGameTypeData.txt: 10 kart race, 11 demolition derby, 12 soccer.
    /// </summary>
    public int MiniGameType { get; set; }

    /// <summary>
    /// The job type the game belongs to: 5 Kart Driver, 7 Demo Derby Driver, 8 Soccer Star.
    /// </summary>
    public int ProfileType { get; set; }

    /// <summary>
    /// That job's Profiles.json id: a player who has it is switched to it on the offer, as live.
    /// </summary>
    public int ProfileId { get; set; }

    public int NameId { get; set; }
    public int DescriptionId { get; set; }
    public int IconId { get; set; }
    public int Difficulty { get; set; } = 1;

    /// <summary>
    /// The game's zone (Zones/*.json); each match gets its own copy of it.
    /// </summary>
    public int ZoneId { get; set; }

    public float[] Entry { get; set; } = [0f, 0f, 0f];

    public float EntryHeading { get; set; }

    /// <summary>
    /// Npcs.json ids that offer the game when clicked.
    /// </summary>
    public List<int> StarterNpcIds { get; set; } = [];

    /// <summary>
    /// The Games window category (16 Karts, 22 Soccer) and tile picture (&lt;name&gt;_detail.dds and _thumb.dds).
    /// </summary>
    public int ActivityCategory { get; set; }
    public string? Image { get; set; }

    /// <summary>
    /// A kart race's track (its zone name in <see cref="VehicleGamesDefinition.Tracks"/>).
    /// </summary>
    public string? Track { get; set; }

    /// <summary>
    /// The start panel's goal, if it has one (soccer: "Beat the other team!").
    /// </summary>
    public int ObjectiveId { get; set; }
    public int ObjectiveNameId { get; set; }
    public int ObjectiveDescriptionId { get; set; }

    /// <summary>
    /// What winning pays; lower places get less.
    /// </summary>
    public int Stars { get; set; }
    public int Coins { get; set; }

    [JsonIgnore]
    public Vector4 EntryPosition => new(Entry[0], Entry[1], Entry[2], 1f);
}

/// <summary>
/// Where a car or kart starts: its position and which way it faces (forward is (sin, 0, cos) of the heading).
/// </summary>
public sealed class VehicleSpawnDefinition
{
    public float[] Position { get; set; } = [0f, 0f, 0f];

    public float Heading { get; set; }

    [JsonIgnore]
    public Vector3 Point => new(Position[0], Position[1], Position[2]);
}

/// <summary>
/// The Wildwood demo derby arena (sg_dm_arena), as live: the player's car, seven AI cars and the power-up boxes.
/// </summary>
public sealed class DerbyArenaDefinition
{
    public float TimeLimitSeconds { get; set; } = 240f;

    /// <summary>
    /// The crown over the leader (sg_ktdm_crown_bbe).
    /// </summary>
    public int CrownModelId { get; set; }

    /// <summary>
    /// The player's car (a VehicleConfigs.json name).
    /// </summary>
    public string PlayerVehicle { get; set; } = string.Empty;

    public VehicleSpawnDefinition PlayerSpawn { get; set; } = new();

    public List<DerbyDriverDefinition> Drivers { get; set; } = [];

    /// <summary>
    /// The arena floor's middle and its half widths along x and z: the rounded rectangle the AI keeps to.
    /// </summary>
    public float[] Centre { get; set; } = [0f, 0f, 0f];
    public float[] Radii { get; set; } = [1f, 1f];

    public int PickupModelId { get; set; }

    public List<VehiclePickupDefinition> Pickups { get; set; } = [];
}

public sealed class DerbyDriverDefinition
{
    /// <summary>
    /// The driver (Npcs.json), whose guid is the car's.
    /// </summary>
    public int NpcId { get; set; }

    /// <summary>
    /// A VehicleConfigs.json name.
    /// </summary>
    public string Vehicle { get; set; } = string.Empty;

    public VehicleSpawnDefinition Spawn { get; set; } = new();
}

public sealed class VehiclePickupDefinition
{
    public int Id { get; set; }

    public float[] Position { get; set; } = [0f, 0f, 0f];

    [JsonIgnore]
    public Vector3 Point => new(Position[0], Position[1], Position[2]);
}

/// <summary>
/// A kart track: its grid, its gates in order (the last one is the finish line), the item boxes and the AI drivers
/// with the lines the live game's AI drove.
/// </summary>
public sealed class RaceTrackDefinition
{
    /// <summary>
    /// The track's zone name.
    /// </summary>
    public string Zone { get; set; } = string.Empty;

    public int Laps { get; set; } = 3;

    /// <summary>
    /// The minimap picture.
    /// </summary>
    public int TrackId { get; set; }

    public float FinishCameraDelay { get; set; }

    /// <summary>
    /// The finish camera's 4x4 transform, row by row.
    /// </summary>
    public float[] FinishCamera { get; set; } = [];

    public int MissileExplosionEffectId { get; set; }
    public int MineExplosionEffectId { get; set; }

    public string PlayerVehicle { get; set; } = string.Empty;

    public int PlayerSlot { get; set; }

    public List<RaceGridSlotDefinition> Grid { get; set; } = [];

    public List<RaceDriverDefinition> Drivers { get; set; } = [];

    /// <summary>
    /// Where the gates are, in the order a lap passes them. The client reports a gate by its kart's position only.
    /// </summary>
    public List<float[]> Gates { get; set; } = [];

    public int PickupModelId { get; set; }
    public float PickupRadius { get; set; } = 1f;
    public int PickupValue { get; set; }

    public List<VehiclePickupDefinition> Pickups { get; set; } = [];
}

public sealed class RaceGridSlotDefinition
{
    public int Slot { get; set; }

    public float[] Position { get; set; } = [0f, 0f, 0f];

    public float Heading { get; set; }

    [JsonIgnore]
    public Vector3 Point => new(Position[0], Position[1], Position[2]);
}

public sealed class RaceDriverDefinition
{
    /// <summary>
    /// The driver (Npcs.json), whose guid is the kart's.
    /// </summary>
    public int NpcId { get; set; }

    public int Slot { get; set; }

    /// <summary>
    /// A VehicleConfigs.json name.
    /// </summary>
    public string Vehicle { get; set; } = string.Empty;

    /// <summary>
    /// The live AI's line from the grid, a little over a lap: seconds after GO, then x, y and z.
    /// </summary>
    public List<float[]> Line { get; set; } = [];
}

/// <summary>
/// The Sacred Grove pitch (sg_soccer): the client config live sent, the teams and their players.
/// </summary>
public sealed class SoccerPitchDefinition
{
    public float PeriodSeconds { get; set; } = 180f;

    /// <summary>
    /// Min x, min z, max x, max z; the goals are at the low and high x ends.
    /// </summary>
    public float[] PitchBounds { get; set; } = [0f, 0f, 0f, 0f];

    public float[] Tuning { get; set; } = [];
    public float[] Tuning2 { get; set; } = [];
    public string BallModel { get; set; } = string.Empty;
    public float Unknown { get; set; }
    public float Unknown2 { get; set; }
    public float[] BallSpawn { get; set; } = [0f, 0f, 0f];
    public float[] Tuning3 { get; set; } = [];
    public uint Unknown3 { get; set; }
    public uint Unknown4 { get; set; }
    public float Unknown5 { get; set; }

    public List<SoccerTeamDefinition> Teams { get; set; } = [];

    /// <summary>
    /// The player's kick-off spot (team 0, position 0).
    /// </summary>
    public float[] PlayerKickOff { get; set; } = [0f, 0f, 0f];

    /// <summary>
    /// The live tuning (hex, 168 bytes) and stats the player had.
    /// </summary>
    public string PlayerOneTimeStats { get; set; } = string.Empty;
    public int[] PlayerStats { get; set; } = [];

    public List<SoccerPlayerDefinition> Players { get; set; } = [];

    /// <summary>
    /// Where power-ups appear, (x, z) each.
    /// </summary>
    public List<float[]> PickupSlots { get; set; } = [];
}

public sealed class SoccerTeamDefinition
{
    public string Tint { get; set; } = string.Empty;

    /// <summary>
    /// 0x00BBGGRR.
    /// </summary>
    public uint Colour { get; set; }

    public int NameId { get; set; }
}

public sealed class SoccerPlayerDefinition
{
    public int NpcId { get; set; }

    public int Team { get; set; }

    /// <summary>
    /// 0-2 the field, 3 the goalie.
    /// </summary>
    public int Position { get; set; }

    public float[] KickOff { get; set; } = [0f, 0f, 0f];

    public string OneTimeStats { get; set; } = string.Empty;

    public int[] Stats { get; set; } = [];
}
