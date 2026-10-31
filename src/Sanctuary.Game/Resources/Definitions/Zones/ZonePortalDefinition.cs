using System;
using System.Numerics;

namespace Sanctuary.Game.Resources.Definitions.Zones;

/// <summary>
/// A stretch of a world zone that sends players on to another zone, like walking through Lavender Coast Pass.
/// </summary>
public sealed class ZonePortalDefinition : ZoneShapeDefinition
{
    public int ToZoneId { get; set; }

    /// <summary>
    /// Where the player arrives (x, y, z). Keep it outside the destination's own portals.
    /// </summary>
    public float[] ToPosition { get; set; } = [];

    /// <summary>
    /// Facing on arrival, in radians, like a point of interest's heading.
    /// </summary>
    public float ToHeading { get; set; }

    public Vector4 Destination(float w) => new(ToPosition[0], ToPosition[1], ToPosition[2], w);

    public Quaternion DestinationRotation() => new(MathF.Sin(ToHeading), 0f, MathF.Cos(ToHeading), 0f);
}
