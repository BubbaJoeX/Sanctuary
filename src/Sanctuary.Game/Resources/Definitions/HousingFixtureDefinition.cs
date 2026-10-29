using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

/// <summary>
/// What a piece of house furniture does besides sitting there: bounce whoever stands on it, carry them
/// along a route, or beam them to another pad.
/// </summary>
public sealed class HousingFixtureDefinition
{
    public List<int> ItemDefinitionIds { get; set; } = [];

    /// <summary>
    /// The actor placed in the house, when the item's own model is only its store preview: a music block's
    /// item names one without collision that plays its note over and over.
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// A global client area (the client's globalclientareas.xml) the fixture's actor carries. Its "floor"
    /// shape applies to whoever stands on the fixture: a trampoline's auto-jump, a bed's bounce, a music
    /// block's note.
    /// </summary>
    public int AreaDefinitionId { get; set; }

    public HousingPlatformDefinition? Platform { get; set; }

    public HousingTeleporterDefinition? Teleporter { get; set; }
}

/// <summary>
/// A platform that rides a looping route, carrying anyone standing on it.
/// </summary>
public sealed class HousingPlatformDefinition
{
    /// <summary>
    /// The route's corners in metres from where the platform was placed (X right, Y up, Z forward), turned
    /// with it. The platform goes through them in order and back to the first.
    /// </summary>
    public List<float[]> Path { get; set; } = [];

    /// <summary>
    /// Metres per second.
    /// </summary>
    public float Speed { get; set; }

    /// <summary>
    /// How long it waits at each corner.
    /// </summary>
    public float PauseSeconds { get; set; }

    /// <summary>
    /// Actor animation (AnimationTypes.xml) whose effects draw the route as a dotted line. It plays while the
    /// house is being decorated, when the platform waits where it was placed.
    /// </summary>
    public int PathAnimationId { get; set; }
}

public enum HousingTeleporterMode
{
    /// <summary>
    /// Pads link in pairs, in the order they were placed, and send each other back and forth.
    /// </summary>
    TwoWay,

    /// <summary>
    /// Pads link in pairs, in the order they were placed, and only the first sends to the second.
    /// </summary>
    OneWay,

    /// <summary>
    /// Every pad sends to a random other one.
    /// </summary>
    MultiWay
}

public sealed class HousingTeleporterDefinition
{
    public HousingTeleporterMode Mode { get; set; }

    /// <summary>
    /// The charge-up and flash played on whoever is beamed away.
    /// </summary>
    public int TeleportEffectId { get; set; }

    /// <summary>
    /// The beam drawn between linked pads while the house is being decorated.
    /// </summary>
    public int LinkEffectId { get; set; }
}
