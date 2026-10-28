using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Pathfinding;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class NavMeshBuilderTests
{
    // Two flat 40 x 40 m islands with a 10 m gap between them along x.
    private static (float[] Vertices, int[] Faces) TwoIslands()
    {
        List<float> vertices = [];
        List<int> faces = [];

        foreach (var x0 in new[] { 0f, 50f })
        {
            var first = vertices.Count / 3;

            vertices.AddRange([x0, 0f, 0f, x0 + 40f, 0f, 0f, x0 + 40f, 0f, 40f, x0, 0f, 40f]);
            faces.AddRange([first, first + 2, first + 1, first, first + 3, first + 2]);
        }

        return ([.. vertices], [.. faces]);
    }

    [TestMethod]
    public void Build_WalksAcrossAnIsland()
    {
        var (vertices, faces) = TwoIslands();
        var navMesh = NavMeshBuilder.Build(vertices, faces);

        Assert.IsNotNull(navMesh);

        var pathfinder = new NavMeshPathfinder(navMesh);
        var waypoints = new Queue<Vector3>();

        Assert.IsTrue(pathfinder.TryFindPath(new Vector3(5f, 0f, 5f), new Vector3(35f, 0f, 35f), waypoints));
        Assert.AreEqual(35f, waypoints.Last().X, 1f);
        Assert.AreEqual(35f, waypoints.Last().Z, 1f);

        Assert.IsTrue(pathfinder.TryWalkStraight(new Vector3(5f, 0f, 20f), new Vector3(35f, 0f, 20f), out var end));
        Assert.AreEqual(35f, end.X, 0.01f);
    }

    [TestMethod]
    public void WalkStraight_StopsAtTheLedge()
    {
        var (vertices, faces) = TwoIslands();
        var pathfinder = new NavMeshPathfinder(NavMeshBuilder.Build(vertices, faces)!);

        // Toward the other island: the walk ends near the first one's edge (x 40, less the agent's width).
        Assert.IsFalse(pathfinder.TryWalkStraight(new Vector3(20f, 0f, 20f), new Vector3(70f, 0f, 20f), out var end));
        Assert.IsTrue(end.X > 35f && end.X <= 40f, $"Stopped at x {end.X}.");

        // And there's no way across.
        var waypoints = new Queue<Vector3>();
        Assert.IsFalse(pathfinder.TryFindPath(new Vector3(20f, 0f, 20f), new Vector3(70f, 0f, 20f), waypoints)
            && waypoints.Last().X > 45f, "No path should reach the other island.");
    }

    [TestMethod]
    public void Build_NothingWalkable_IsNull()
    {
        Assert.IsNull(NavMeshBuilder.Build([], []));

        // A vertical wall only.
        Assert.IsNull(NavMeshBuilder.Build([0f, 0f, 0f, 10f, 0f, 0f, 10f, 10f, 0f, 0f, 10f, 0f], [0, 1, 2, 0, 2, 3]));
    }

    [TestMethod]
    public void Save_LoadsBackWithNavMeshLoader()
    {
        var (vertices, faces) = TwoIslands();
        var path = Path.Combine(Path.GetTempPath(), $"islands-{Guid.NewGuid():N}.navmesh");

        try
        {
            NavMeshBuilder.Save(path, NavMeshBuilder.Build(vertices, faces)!);

            Assert.IsTrue(NavMeshLoader.TryLoad(path, NullLogger.Instance, out var loaded));

            var waypoints = new Queue<Vector3>();
            Assert.IsTrue(new NavMeshPathfinder(loaded!).TryFindPath(new Vector3(55f, 0f, 5f), new Vector3(85f, 0f, 35f), waypoints));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
