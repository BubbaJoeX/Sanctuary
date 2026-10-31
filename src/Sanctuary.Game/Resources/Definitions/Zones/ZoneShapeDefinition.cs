using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Core.Extensions;

namespace Sanctuary.Game.Resources.Definitions.Zones;

/// <summary>
/// A stretch of a world zone, in the same fields as <see cref="ZoneAreaDefinition"/>: Circle (X1, Z1, Radius),
/// Rectangle (X1, Z1 to X2, Z2) or Polygon (Points).
/// </summary>
public abstract class ZoneShapeDefinition
{
    public string? Comment { get; set; }

    public ZoneAreaShape Shape { get; set; }

    public float X1 { get; set; }
    public float Z1 { get; set; }

    public float X2 { get; set; }
    public float Z2 { get; set; }

    public float Radius { get; set; }

    public List<Vector3> Points { get; set; } = [];

    public bool Contains(Vector4 position)
    {
        return Shape switch
        {
            ZoneAreaShape.Circle => position.IsInCircle(new Vector3(X1, 0, Z1), Radius),
            ZoneAreaShape.Rectangle => position.IsInRectangle(new Vector3(X1, 0, Z1), new Vector3(X2, 0, Z2)),
            ZoneAreaShape.Polygon => position.IsInPolygon(Points),
            _ => false
        };
    }
}
