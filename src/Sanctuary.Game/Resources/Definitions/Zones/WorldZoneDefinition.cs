using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions.Zones;

public sealed class WorldZoneDefinition : BaseZoneDefinition
{
    public List<ZoneAreaDefinition> AreaDefinitions { get; set; } = [];

    public List<int> EnabledClientAreaIds { get; set; } = [];

    public List<SeasonalClientAreaDefinition> SeasonalClientAreas { get; set; } = [];

    public List<ZonePortalDefinition> Portals { get; set; } = [];

    /// <summary>
    /// Stretches that are a combat area, or aren't one, whatever area they're in: where hostile NPCs stand in a
    /// town, like Sanctuary's bixie fields, and a town's edge that's on the wilds' tiles. The first that holds a
    /// position decides.
    /// </summary>
    public List<ZoneCombatAreaDefinition> CombatAreas { get; set; } = [];
}