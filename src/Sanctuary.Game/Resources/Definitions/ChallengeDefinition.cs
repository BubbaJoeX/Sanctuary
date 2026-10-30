using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

/// <summary>
/// The client's in-world minigames (MiniGameData.txt type 1): timed challenges an NPC in the open world offers,
/// played with the minigame HUD. Only their names and descriptions shipped; what each one asks for (Challenges.json)
/// is read from its description, and what it uses is placed on the navmesh around the NPC as it starts.
/// </summary>
public sealed class ChallengeDefinition
{
    /// <summary>
    /// Its MiniGameData.txt id, the stage id the client knows it by.
    /// </summary>
    public int Id { get; set; }

    public string? Comment { get; set; }

    public int NameId { get; set; }
    public int DescriptionId { get; set; }
    public int IconId { get; set; }
    public int Difficulty { get; set; } = 1;

    /// <summary>
    /// The Npcs.json NPC in Fabled Realms who offers it, and whom Return challenges bring things back to.
    /// </summary>
    public int GiverNpcId { get; set; }

    public ChallengeKind Kind { get; set; }

    /// <summary>
    /// How many things to collect, bring back or catch, flags to run through, or people to deliver to.
    /// </summary>
    public int Count { get; set; } = 8;

    public int TimeLimitSeconds { get; set; } = 120;

    /// <summary>
    /// How far from the giver its things are placed (or a race runs).
    /// </summary>
    public float Radius { get; set; } = 40f;

    /// <summary>
    /// The Models.txt model of what's collected or caught. Races use the green and red flags.
    /// </summary>
    public int ModelId { get; set; }

    /// <summary>
    /// For Deliver: the Npcs.json NPCs to go to, the nearest of each to the giver.
    /// </summary>
    public List<int> RecipientNpcIds { get; set; } = [];

    /// <summary>
    /// For Race: the time to beat, in seconds. Finishing in the time limit but slower still pays half.
    /// </summary>
    public int RecordSeconds { get; set; }

    /// <summary>
    /// The job (a Profiles.json id) whose stars it pays; 0 for whichever job the player has on.
    /// </summary>
    public int ProfileId { get; set; }

    public int Stars { get; set; } = 30;
    public int Coins { get; set; } = 50;
}

public enum ChallengeKind
{
    /// <summary>
    /// Pick up everything scattered around, by clicking it or walking over it.
    /// </summary>
    Collect,

    /// <summary>
    /// Pick things up one at a time and bring each one back to the giver.
    /// </summary>
    Return,

    /// <summary>
    /// Run through the green flags in order to the red one.
    /// </summary>
    Race,

    /// <summary>
    /// Go to each of the people the giver names.
    /// </summary>
    Deliver,

    /// <summary>
    /// Catch the animals (or the ninja) wandering about, by clicking them or running into them.
    /// </summary>
    Catch
}
