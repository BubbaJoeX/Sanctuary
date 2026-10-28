using System.Collections.Generic;
using System.Numerics;

using DotRecast.Core.Numerics;
using DotRecast.Detour;

namespace Sanctuary.Game.Pathfinding;

public sealed class NavMeshPathfinder
{
    private const int MaxPathPolys = 256;
    private const int MaxSearchIterations = 2048;
    private const int MaxStraightPathPoints = 64;

    private static readonly RcVec3f NearestPolyExtents = new(2f, 4f, 2f);

    private readonly object _lock = new();
    private readonly DtNavMeshQuery _query;
    private readonly DtQueryDefaultFilter _filter = new();
    private readonly long[] _pathPolys = new long[MaxPathPolys];
    private readonly DtStraightPath[] _straightPath = new DtStraightPath[MaxStraightPathPoints];

    public NavMeshPathfinder(DtNavMesh navMesh)
    {
        _query = new DtNavMeshQuery(navMesh);
    }

    /// <summary>
    /// Walks the navmesh in a straight line from <paramref name="start"/> toward <paramref name="goal"/>. True when
    /// nothing is in the way; otherwise <paramref name="end"/> is the last walkable point before the wall or edge
    /// (the start itself when it isn't on the navmesh).
    /// </summary>
    public bool TryWalkStraight(Vector3 start, Vector3 goal, out Vector3 end)
    {
        end = start;

        var startPosition = new RcVec3f(start.X, start.Y, start.Z);
        var goalPosition = new RcVec3f(goal.X, goal.Y, goal.Z);

        lock (_lock)
        {
            _query.FindNearestPoly(startPosition, NearestPolyExtents, _filter, out var startRef, out var startOnMesh, out _);

            if (startRef == 0)
                return false;

            var status = _query.Raycast(startRef, startOnMesh, goalPosition, _filter, out var t, out _, _pathPolys, out var pathCount, MaxPathPolys);

            if (status.Failed())
                return false;

            // A t past 1 (float.MaxValue) means the ray reached the goal.
            if (t >= 1f)
            {
                end = goal;
                return true;
            }

            var hit = RcVec3f.Lerp(startOnMesh, goalPosition, t);

            if (pathCount > 0 && _query.GetPolyHeight(_pathPolys[pathCount - 1], hit, out var height).Succeeded())
                hit.Y = height;

            end = new Vector3(hit.X, hit.Y, hit.Z);
            return false;
        }
    }

    public bool TryFindPath(Vector3 start, Vector3 goal, Queue<Vector3> waypoints)
    {
        var startPosition = new RcVec3f(start.X, start.Y, start.Z);
        var goalPosition = new RcVec3f(goal.X, goal.Y, goal.Z);

        lock (_lock)
        {
            _query.FindNearestPoly(startPosition, NearestPolyExtents, _filter, out var startRef, out var startOnMesh, out _);
            _query.FindNearestPoly(goalPosition, NearestPolyExtents, _filter, out var goalRef, out var goalOnMesh, out _);

            if (startRef == 0 || goalRef == 0)
                return false;

            var status = _query.InitSlicedFindPath(startRef, goalRef, startOnMesh, goalOnMesh, _filter, 0);
            if (status.Failed())
                return false;

            _query.UpdateSlicedFindPath(MaxSearchIterations, out _);

            status = _query.FinalizeSlicedFindPath(_pathPolys, out var pathCount, MaxPathPolys);
            if (status.Failed() || pathCount == 0)
                return false;

            var endPosition = goalOnMesh;
            if (_pathPolys[pathCount - 1] != goalRef)
                _query.ClosestPointOnPoly(_pathPolys[pathCount - 1], goalOnMesh, out endPosition, out _);

            status = _query.FindStraightPath(startOnMesh, endPosition, _pathPolys, pathCount, _straightPath, out var straightPathCount,
                MaxStraightPathPoints, 0);
            if (status.Failed() || straightPathCount == 0)
                return false;

            waypoints.Clear();

            for (var point = 1; point < straightPathCount; point++)
            {
                var position = _straightPath[point].pos;
                waypoints.Enqueue(new Vector3(position.X, position.Y, position.Z));
            }

            return waypoints.Count > 0;
        }
    }
}
