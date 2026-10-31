using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.Battles;

public enum BattleState
{
    /// <summary>
    /// The start panel is up; nobody has gone in yet.
    /// </summary>
    Offered,

    Running,

    /// <summary>
    /// The last enemy fell: the Exit is up.
    /// </summary>
    Cleared,

    /// <summary>
    /// The end screen is up; players leave as they close it.
    /// </summary>
    Over,

    Closed
}

/// <summary>
/// One battle being fought: its own copy of the battle's zone, the players in it and its waves. Changed only under
/// its own lock.
/// </summary>
public sealed class Battle(int id, BattleDefinition definition, WorldZone zone)
{
    /// <summary>
    /// The encounter id the client is given (the instance number over the battle id, as live).
    /// </summary>
    public int Id { get; } = id;

    public BattleDefinition Definition { get; } = definition;

    public WorldZone Zone { get; } = zone;

    public BattleState State { get; set; }

    public Dictionary<ulong, BattleParticipant> Participants { get; } = [];

    /// <summary>
    /// Group members asked along who haven't answered.
    /// </summary>
    public HashSet<ulong> Invited { get; } = [];

    public long OfferedAt { get; set; }
    public long StartedAt { get; set; }
    public long EndsAt { get; set; }
    public long OverAt { get; set; }

    /// <summary>
    /// The next wave waiting for its turn, and when it comes.
    /// </summary>
    public int NextWave { get; set; }
    public long NextWaveAt { get; set; }

    public List<Npc> Alive { get; } = [];

    public int Total { get; set; }
    public int Defeated { get; set; }
    public int Knockouts { get; set; }

    public Npc? Boss { get; set; }
    public bool BossDefeated { get; set; }

    public Vector4 LastKillPosition { get; set; }

    public Npc? Exit { get; set; }

    /// <summary>
    /// The Exit behind the entry, up from the start: the way out before the goal's done (giving the battle up), and
    /// after it, as the other Exit is.
    /// </summary>
    public Npc? EntryExit { get; set; }

    public bool Won { get; set; }

    public Vector4 EntryPosition => new(Definition.Entry[0], Definition.Entry[1], Definition.Entry[2], 1f);
}

public sealed class BattleParticipant(Player player)
{
    public Player Player { get; } = player;

    public bool Entered { get; set; }

    /// <summary>
    /// The HUD is up (the player has loaded in).
    /// </summary>
    public bool Arrived { get; set; }

    public bool Left { get; set; }

    public bool BossBarShown { get; set; }

    /// <summary>
    /// The job the battle switched them out of for a combat job (0 if it didn't), given back as they leave.
    /// </summary>
    public int PreviousProfileId { get; set; }

    /// <summary>
    /// Where they were when they went in, to go back to.
    /// </summary>
    public int ReturnZoneId { get; set; }
    public ulong? ReturnOwnerId { get; set; }
    public Vector4 ReturnPosition { get; set; }
    public Quaternion ReturnRotation { get; set; }
}
