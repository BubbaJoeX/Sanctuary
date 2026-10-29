using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sanctuary.Game.Housing;

/// <summary>
/// A moving platform's loop: it waits at each corner, then travels in a straight line to the next, and from
/// the last back to the first. Where it is follows from the time alone, so every viewer agrees.
/// </summary>
public sealed class PlatformRoute
{
    private readonly Vector3[] _points;
    private readonly float[] _travelSeconds;
    private readonly float _pauseSeconds;

    public float CycleSeconds { get; }

    public Vector3 Start => _points[0];

    public PlatformRoute(IReadOnlyList<Vector3> points, float speed, float pauseSeconds)
    {
        if (points.Count < 2)
            throw new ArgumentException("A route needs at least two points.", nameof(points));

        if (speed <= 0f)
            throw new ArgumentOutOfRangeException(nameof(speed));

        _points = [.. points];
        _pauseSeconds = Math.Max(0f, pauseSeconds);
        _travelSeconds = new float[_points.Length];

        for (var i = 0; i < _points.Length; i++)
        {
            _travelSeconds[i] = Vector3.Distance(_points[i], _points[(i + 1) % _points.Length]) / speed;
            CycleSeconds += _pauseSeconds + _travelSeconds[i];
        }
    }

    /// <summary>
    /// The route of a platform placed at <paramref name="origin"/> facing <paramref name="heading"/>, from
    /// corners given in its own space (X right, Y up, Z forward).
    /// </summary>
    public static PlatformRoute FromLocal(IEnumerable<float[]> localPoints, Vector3 origin, float heading, float speed, float pauseSeconds)
    {
        var forward = new Vector3(MathF.Sin(heading), 0f, MathF.Cos(heading));
        var right = new Vector3(forward.Z, 0f, -forward.X);

        var points = new List<Vector3>();

        foreach (var point in localPoints)
            points.Add(origin + right * point[0] + Vector3.UnitY * point[1] + forward * point[2]);

        return new PlatformRoute(points, speed, pauseSeconds);
    }

    /// <summary>
    /// Where the platform is <paramref name="seconds"/> into its loop, and whether it's travelling.
    /// </summary>
    public (Vector3 Position, bool Moving) Sample(float seconds)
    {
        if (CycleSeconds <= 0f)
            return (_points[0], false);

        var time = seconds % CycleSeconds;

        if (time < 0f)
            time += CycleSeconds;

        for (var i = 0; i < _points.Length; i++)
        {
            if (time < _pauseSeconds)
                return (_points[i], false);

            time -= _pauseSeconds;

            if (time < _travelSeconds[i])
            {
                var next = _points[(i + 1) % _points.Length];
                return (Vector3.Lerp(_points[i], next, time / _travelSeconds[i]), true);
            }

            time -= _travelSeconds[i];
        }

        return (_points[0], false);
    }
}
