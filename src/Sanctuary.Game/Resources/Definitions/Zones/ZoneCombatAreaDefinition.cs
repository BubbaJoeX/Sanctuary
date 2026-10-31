namespace Sanctuary.Game.Resources.Definitions.Zones;

/// <summary>
/// A stretch of a world zone that is a combat area, or isn't one, whatever area it's in (see
/// <see cref="ZoneAreaDefinition.IsCombatArea"/>).
/// </summary>
public sealed class ZoneCombatAreaDefinition : ZoneShapeDefinition
{
    public bool IsCombatArea { get; set; } = true;
}
