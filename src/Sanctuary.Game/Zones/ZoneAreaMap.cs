using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.Zones;

namespace Sanctuary.Game.Zones;

/// <summary>
/// Which of a world zone's areas a position is in: an area's own tiles first, then its shape, then the terrain
/// environment of the tile.
/// </summary>
public sealed class ZoneAreaMap
{
    private readonly WorldZoneDefinition _zoneDefinition;

    private readonly Dictionary<(int Longitude, int Latitude), ZoneAreaDefinition> _tileZoneAreas = [];
    private readonly List<ZoneAreaDefinition> _shapeZoneAreas = [];
    private readonly Dictionary<int, List<ZoneAreaDefinition>> _environmentZoneAreas = [];

    public ZoneAreaMap(WorldZoneDefinition zoneDefinition)
    {
        _zoneDefinition = zoneDefinition;

        foreach (var areaDefinition in _zoneDefinition.AreaDefinitions)
        {
            if (areaDefinition.Tiles.Count > 0)
            {
                foreach (var tile in areaDefinition.Tiles)
                    _tileZoneAreas[(tile[0], tile[1])] = areaDefinition;

                continue;
            }

            if (areaDefinition.EnvironmentIds.Count == 0)
            {
                _shapeZoneAreas.Add(areaDefinition);
                continue;
            }

            foreach (var environmentId in areaDefinition.EnvironmentIds)
            {
                if (!_environmentZoneAreas.TryGetValue(environmentId, out var environmentZoneAreas))
                {
                    environmentZoneAreas = [];
                    _environmentZoneAreas[environmentId] = environmentZoneAreas;
                }

                environmentZoneAreas.Add(areaDefinition);
            }
        }
    }

    public ZoneAreaDefinition? GetZoneArea(Vector4 position, ZoneTile zoneTile)
    {
        if (_tileZoneAreas.TryGetValue((zoneTile.Longitude, zoneTile.Latitude), out var tileZoneArea))
            return tileZoneArea;

        foreach (var areaDefinition in _shapeZoneAreas)
        {
            var isInArea = areaDefinition.Shape switch
            {
                ZoneAreaShape.Circle => position.IsInCircle(new Vector3(areaDefinition.X1, 0, areaDefinition.Z1), areaDefinition.Radius),
                ZoneAreaShape.Rectangle => position.IsInRectangle(new Vector3(areaDefinition.X1, 0, areaDefinition.Z1),
                    new Vector3(areaDefinition.X2, 0, areaDefinition.Z2)),
                ZoneAreaShape.Polygon => position.IsInPolygon(areaDefinition.Points),
                _ => throw new NotImplementedException(nameof(areaDefinition.Shape))
            };

            if (isInArea)
                return areaDefinition;
        }

        if (!_environmentZoneAreas.TryGetValue(zoneTile.EnvironmentId, out var environmentZoneAreas))
            return null;

        return environmentZoneAreas.FirstOrDefault(x => x.IsInEnvironmentXRange(position.X));
    }

    /// <summary>
    /// Whether <paramref name="position"/>, in <paramref name="area"/>, is a combat area: as the zone's combat areas
    /// say if one holds it, or else as the area is.
    /// </summary>
    public bool IsCombatArea(Vector4 position, ZoneAreaDefinition? area)
    {
        var combatArea = _zoneDefinition.CombatAreas.FirstOrDefault(x => x.Contains(position));

        return combatArea?.IsCombatArea ?? area?.IsCombatArea == true;
    }
}
