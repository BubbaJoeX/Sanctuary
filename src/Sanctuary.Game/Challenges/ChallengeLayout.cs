using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Sanctuary.Core.Extensions;

namespace Sanctuary.Game.Challenges;

/// <summary>
/// Where a challenge's things go, around the NPC who offers it. Points come from <c>sample</c>, a walkable point
/// near a spot (the zone's navmesh), or null where there's none.
/// </summary>
public static class ChallengeLayout
{
    /// <summary>
    /// Nothing is put closer to the NPC than this, so it isn't picked up on the spot.
    /// </summary>
    public const float MinDistanceFromCenter = 6f;

    public const float MinSpacing = 4f;

    private const int AttemptsPerPoint = 12;

    /// <summary>
    /// Up to <paramref name="count"/> points scattered within <paramref name="radius"/>, apart from each other and
    /// from the center.
    /// </summary>
    public static List<Vector3> Scatter(Vector3 center, float radius, int count, Func<Vector3, float, Vector3?> sample)
    {
        var points = new List<Vector3>();

        for (var attempt = 0; attempt < count * AttemptsPerPoint && points.Count < count; attempt++)
        {
            if (sample(center, radius) is not { } point)
                continue;

            if (point.DistanceSquaredXZ(center) < MinDistanceFromCenter * MinDistanceFromCenter)
                continue;

            if (points.Any(other => other.DistanceSquaredXZ(point) < MinSpacing * MinSpacing))
                continue;

            points.Add(point);
        }

        return points;
    }

    /// <summary>
    /// A race's flags: a loop out round the center and back to it, the last one by the NPC.
    /// </summary>
    public static List<Vector3> RaceLoop(Vector3 center, float radius, int count, float startAngle, Func<Vector3, float, Vector3?> sample)
    {
        var flags = new List<Vector3>();

        for (var i = 0; i < count - 1; i++)
        {
            var angle = startAngle + MathF.Tau * i / (count - 1);

            // Out to the edge and back, so the loop isn't a plain circle.
            var distance = radius * (0.6f + 0.4f * MathF.Sin(MathF.PI * (i + 1) / count));

            var spot = center + new Vector3(MathF.Sin(angle) * distance, 0f, MathF.Cos(angle) * distance);

            if (sample(spot, radius * 0.15f) is { } flag && flag.DistanceSquaredXZ(center) >= MinDistanceFromCenter * MinDistanceFromCenter)
                flags.Add(flag);
        }

        // The finish, beside the NPC.
        if (sample(center + new Vector3(0f, 0f, MinDistanceFromCenter), 2f) is { } finish)
            flags.Add(finish);

        return flags;
    }

    /// <summary>
    /// How far out from the center a challenge goes: its radius, or as far as the farthest of
    /// <paramref name="points"/> (the people a delivery goes to).
    /// </summary>
    public static float Reach(Vector3 center, float radius, IEnumerable<Vector3> points) =>
        Math.Max(radius, points.Select(point => Vector3.Distance(center, point)).DefaultIfEmpty(0f).Max());

    /// <summary>
    /// The index of the point nearest to <paramref name="from"/> that <paramref name="isOpen"/> keeps, or -1.
    /// </summary>
    public static int Nearest(Vector3 from, IReadOnlyList<Vector3> points, Func<int, bool> isOpen)
    {
        var best = -1;
        var bestDistance = float.MaxValue;

        for (var i = 0; i < points.Count; i++)
        {
            if (!isOpen(i))
                continue;

            var distance = from.DistanceSquaredXZ(points[i]);

            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }
}
