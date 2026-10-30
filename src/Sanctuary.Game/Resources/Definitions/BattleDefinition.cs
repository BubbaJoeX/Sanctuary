using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

/// <summary>
/// A battle (see Battles.json): a private copy of a combat zone the player (and their group) fights through in
/// waves of enemies, offered by clicking a dungeon's door or a wandering creature in the world, or from the Games
/// window. Clearing the last wave puts an Exit in the zone; going through it ends the battle and pays out.
/// </summary>
public sealed class BattleDefinition
{
    public int Id { get; set; }

    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// The battle's zone (Zones/*.json); each battle gets its own copy of it.
    /// </summary>
    public int ZoneId { get; set; }

    public int NameId { get; set; }
    public int DescriptionId { get; set; }

    /// <summary>
    /// The start panel's picture (an Images.txt id, icon_ui_combatstart_panelgraphic_*).
    /// </summary>
    public int IconId { get; set; }

    /// <summary>
    /// 1 to 5: the start panel's difficulty and the level range the client shows.
    /// </summary>
    public int Difficulty { get; set; } = 1;

    /// <summary>
    /// Npcs.json ids that offer the battle when clicked: a dungeon's door, a creature roaming the world.
    /// </summary>
    public List<int> StarterNpcIds { get; set; } = [];

    /// <summary>
    /// The Games window's Battles tile picture (the client's &lt;name&gt;_detail.dds and &lt;name&gt;_thumb.dds);
    /// battles without one aren't listed there.
    /// </summary>
    public string? Image { get; set; }

    public int TimeLimitSeconds { get; set; } = 1200;

    /// <summary>
    /// The knocked out window's countdown; players come back at <see cref="Entry"/>.
    /// </summary>
    public int RespawnSeconds { get; set; } = 10;

    /// <summary>
    /// Knocked out this many times between them, the players lose the battle.
    /// </summary>
    public int MaxKnockouts { get; set; } = 5;

    /// <summary>
    /// Group members who can come along, the leader included.
    /// </summary>
    public int MaxPlayers { get; set; } = 4;

    /// <summary>
    /// Where players arrive (x, y, z) and face, in radians.
    /// </summary>
    public float[] Entry { get; set; } = [];
    public float EntryHeading { get; set; }

    /// <summary>
    /// Where the Exit appears once the battle is won; by default where the last enemy fell.
    /// </summary>
    public float[]? Exit { get; set; }

    /// <summary>
    /// The goal on the start panel and the tracker, e.g. "Scare away the wolves!" / "Defeat all of the Frostfang
    /// Growlers."; it counts every enemy.
    /// </summary>
    public int ObjectiveNameId { get; set; }
    public int ObjectiveDescriptionId { get; set; }

    /// <summary>
    /// Optional second goal for the boss (the waves' Boss spawn), e.g. "Defeat Muggenstomp".
    /// </summary>
    public int BossObjectiveNameId { get; set; }
    public int BossObjectiveDescriptionId { get; set; }

    public List<BattleWaveDefinition> Waves { get; set; } = [];

    /// <summary>
    /// Paid to each player who wins, on top of what each enemy pays as it falls. Stars go to the player's combat job.
    /// </summary>
    public int Stars { get; set; }
    public int Coins { get; set; }

    /// <summary>
    /// Optional Rewards.json table each winner rolls once.
    /// </summary>
    public string? RewardTable { get; set; }
}

/// <summary>
/// A group of enemies that comes together. The first wave comes <see cref="DelayMs"/> after the first player
/// arrives, each later one that long after the one before it falls, unless it's <see cref="AtStart"/>: posted
/// guards, there from the start.
/// </summary>
public sealed class BattleWaveDefinition
{
    public int DelayMs { get; set; } = 1500;

    public bool AtStart { get; set; }

    public List<BattleSpawnDefinition> Spawns { get; set; } = [];
}

public sealed class BattleSpawnDefinition
{
    /// <summary>
    /// The enemy's Npcs.json definition (its model, name and size); it fights whatever its disposition there.
    /// </summary>
    public int NpcId { get; set; }

    /// <summary>
    /// Its Mobs.json stats; "default" if not given.
    /// </summary>
    public string? Mob { get; set; }

    public float[] Position { get; set; } = [];
    public float Heading { get; set; }

    /// <summary>
    /// Gets the boss health bar (and the boss goal, if the battle has one).
    /// </summary>
    public bool Boss { get; set; }
}
