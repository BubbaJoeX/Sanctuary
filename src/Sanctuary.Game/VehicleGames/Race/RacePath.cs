using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sanctuary.Game.VehicleGames.Race;

/// <summary>
/// A line round (or along) a track: points with the speed driven through each, smoothed into a curve and measured
/// by distance along it.
/// </summary>
public sealed class RacePath
{
    private readonly Vector3[] _points;
    private readonly float[] _speeds;
    private readonly float[] _distances;

    public RacePath(IReadOnlyList<Vector3> points, IReadOnlyList<float> speeds, bool closed)
    {
        if (points.Count < 2 || points.Count != speeds.Count)
            throw new ArgumentException("A path needs two points or more, each with a speed.");

        _points = [.. points];
        _speeds = [.. speeds];
        Closed = closed;

        var count = closed ? _points.Length + 1 : _points.Length;

        _distances = new float[count];

        for (var i = 1; i < count; i++)
            _distances[i] = _distances[i - 1] + Vector3.Distance(Point(i - 1), Point(i));

        Length = _distances[^1];
    }

    public bool Closed { get; }

    /// <summary>
    /// Its length in metres (a closed path's lap).
    /// </summary>
    public float Length { get; }

    public int Count => _points.Length;

    public IReadOnlyList<Vector3> Points => _points;

    /// <summary>
    /// How far along it a point of it is.
    /// </summary>
    public float DistanceOf(int pointIndex) => _distances[pointIndex];

    private Vector3 Point(int index) => Closed
        ? _points[(index % _points.Length + _points.Length) % _points.Length]
        : _points[Math.Clamp(index, 0, _points.Length - 1)];

    private float Speed(int index) => Closed
        ? _speeds[(index % _speeds.Length + _speeds.Length) % _speeds.Length]
        : _speeds[Math.Clamp(index, 0, _speeds.Length - 1)];

    /// <summary>
    /// A distance along it, wrapped round a closed path and kept to an open one.
    /// </summary>
    public float Wrap(float distance) => Closed
        ? (distance % Length + Length) % Length
        : Math.Clamp(distance, 0f, Length);

    /// <summary>
    /// The segment a distance falls in, and how far along it.
    /// </summary>
    private (int Segment, float Fraction) Locate(float distance)
    {
        distance = Wrap(distance);

        var low = 0;
        var high = _distances.Length - 1;

        while (high - low > 1)
        {
            var middle = (low + high) / 2;

            if (_distances[middle] <= distance)
                low = middle;
            else
                high = middle;
        }

        var span = _distances[high] - _distances[low];

        return (low, span > 1e-5f ? (distance - _distances[low]) / span : 0f);
    }

    /// <summary>
    /// Where along the curve (Catmull-Rom through the points) a distance is.
    /// </summary>
    public Vector3 PositionAt(float distance)
    {
        var (segment, t) = Locate(distance);

        return CatmullRom(Point(segment - 1), Point(segment), Point(segment + 1), Point(segment + 2), t);
    }

    /// <summary>
    /// Which way the curve runs there (normalised).
    /// </summary>
    public Vector3 DirectionAt(float distance)
    {
        var ahead = PositionAt(distance + 1f) - PositionAt(distance - 1f);

        return ahead.LengthSquared() > 1e-6f ? Vector3.Normalize(ahead) : Vector3.UnitZ;
    }

    public float SpeedAt(float distance)
    {
        var (segment, t) = Locate(distance);

        return Speed(segment) + (Speed(segment + 1) - Speed(segment)) * t;
    }

    /// <summary>
    /// The distance along the path nearest a point, searched within <paramref name="window"/> of
    /// <paramref name="near"/> (the whole path without one): a kart is found where it was a moment ago, not on a
    /// stretch of track that passes over or beside it.
    /// </summary>
    public float Project(Vector3 point, float near = 0f, float window = float.MaxValue)
    {
        var bestDistance = near;
        var bestSquared = float.MaxValue;

        var segments = Closed ? _points.Length : _points.Length - 1;

        for (var i = 0; i < segments; i++)
        {
            var a = Point(i);
            var b = Point(i + 1);
            var ab = b - a;
            var lengthSquared = ab.LengthSquared();

            var t = lengthSquared > 1e-6f ? Math.Clamp(Vector3.Dot(point - a, ab) / lengthSquared, 0f, 1f) : 0f;
            var along = _distances[i] + (_distances[i + 1] - _distances[i]) * t;

            if (window < Length && MathF.Abs(Offset(near, along)) > window)
                continue;

            var squared = Vector3.DistanceSquared(point, a + ab * t);

            if (squared < bestSquared)
            {
                bestSquared = squared;
                bestDistance = along;
            }
        }

        return Wrap(bestDistance);
    }

    /// <summary>
    /// From one distance to another the short way (round a closed path).
    /// </summary>
    public float Offset(float from, float to)
    {
        var offset = to - from;

        if (!Closed)
            return offset;

        if (offset > Length / 2f)
            offset -= Length;
        else if (offset < -Length / 2f)
            offset += Length;

        return offset;
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;

        return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
    }
}
