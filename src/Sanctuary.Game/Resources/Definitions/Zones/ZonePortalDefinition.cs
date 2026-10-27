using System;
using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Core.Extensions;

namespace Sanctuary.Game.Resources.Definitions.Zones;

/// <summary>
/// A stretch of a world zone that sends players on to another zone, like walking through Lavender Coast Pass.
/// The shape uses the same fields as <see cref="ZoneAreaDefinition"/>: Circle (X1, Z1, Radius), Rectangle
/// (X1, Z1 to X2, Z2) or Polygon (Points).
/// </summary>
public sealed class ZonePortalDefinition
{
    public string? Comment { get; set; }

    public ZoneAreaShape Shape { get; set; }

    public float X1 { get; set; }
    public float Z1 { get; set; }

    public float X2 { get; set; }
    public float Z2 { get; set; }

    public float Radius { get; set; }

    public List<Vector3> Points { get; set; } = [];

    public int ToZoneId { get; set; }

    /// <summary>
    /// Where the player arrives (x, y, z). Keep it outside the destination's own portals.
    /// </summary>
    public float[] ToPosition { get; set; } = [];

    /// <summary>
    /// Facing on arrival, in radians, like a point of interest's heading.
    /// </summary>
    public float ToHeading { get; set; }

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

    public Vector4 Destination(float w) => new(ToPosition[0], ToPosition[1], ToPosition[2], w);

    public Quaternion DestinationRotation() => new(MathF.Sin(ToHeading), 0f, MathF.Cos(ToHeading), 0f);
}
