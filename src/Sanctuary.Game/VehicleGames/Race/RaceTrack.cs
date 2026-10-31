using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.VehicleGames.Race;

/// <summary>
/// A kart track, built from the live race: its centre line (one AI's lap), its gates and each AI driver's own line.
/// <para>
/// The live AI lines start on the grid, run up the start lane to the first gate, join the track and come round to
/// the finish (the last gate). The next lap leaves the finish up the main straight instead and joins where the start
/// lane did, so the first gate is only ever passed on the first lap. A lap line is the centre line's driver's stretch
/// from the finish to that join, then each driver's own lap from there; distances are measured round the centre
/// line from the finish.
/// </para>
/// </summary>
public sealed class RaceTrack
{
    /// <summary>
    /// A lap counts only through this share of its gates, in order (shortcuts don't).
    /// </summary>
    public const float GatesForALap = 0.75f;

    // A kart is looked for this far either side of where it was.
    private const float ProjectionWindow = 150f;

    private readonly Dictionary<int, DriverLine> _lines = [];

    public RaceTrack(RaceTrackDefinition definition)
    {
        Definition = definition;
        Gates = definition.Gates.Select(gate => new Vector3(gate[0], gate[1], gate[2])).ToList();

        var finish = Gates[^1];

        var centreDriver = definition.Drivers.FirstOrDefault(driver => driver.Slot == definition.CentrelineSlot) ?? definition.Drivers[0];
        var centre = Points(centreDriver.Line);
        var centreFinish = FinishIndex(centre, finish);

        // From the finish up the main straight, as far as the line goes into the next lap.
        var straight = centre.Skip(centreFinish).ToList();
        var join = JoinIndex(centre, centreFinish, straight[^1].Position);

        Loop = Path([.. straight, .. centre.Skip(join).Take(centreFinish - join)], closed: true);
        GateDistances = Gates.Select(gate => Loop.Project(gate)).ToArray();

        foreach (var driver in definition.Drivers)
        {
            var line = Points(driver.Line);
            var ownFinish = FinishIndex(line, finish);
            var ownJoin = JoinIndex(line, ownFinish, straight[^1].Position);

            // A line that ends short of the finish is finished off along the centre line.
            var lap = new List<LinePoint>(straight);
            lap.AddRange(line.Skip(ownJoin).Take(ownFinish - ownJoin + 1));

            if (Vector3.Distance(line[ownFinish].Position, finish) > 25f)
            {
                var rejoin = Nearest(centre, line[ownFinish].Position, join, centreFinish);
                lap.AddRange(centre.Skip(rejoin + 1).Take(centreFinish - rejoin - 1));
            }
            else
            {
                lap.RemoveAt(lap.Count - 1);
            }

            var loop = Path(lap, closed: true);
            var start = Path(line.Take(ownJoin + 1).ToList(), closed: false);

            _lines[driver.Slot] = new DriverLine(start, loop, loop.DistanceOf(straight.Count));
        }
    }

    public RaceTrackDefinition Definition { get; }

    /// <summary>
    /// The centre line, a closed lap from the finish.
    /// </summary>
    public RacePath Loop { get; }

    public float LapLength => Loop.Length;

    public IReadOnlyList<Vector3> Gates { get; }

    /// <summary>
    /// Where each gate is round the centre line.
    /// </summary>
    public float[] GateDistances { get; }

    public int FinishGate => Gates.Count - 1;

    public bool TryGetLine(int slot, out DriverLine line) => _lines.TryGetValue(slot, out line!);

    #region Progress

    /// <summary>
    /// A kart has moved to <paramref name="position"/>: where it is round the centre line, the gates it went
    /// through on the way, and a lap when it went through the finish having been round.
    /// </summary>
    public void Advance(RaceProgress progress, Vector3 position, long now)
    {
        var distance = progress.Tracked
            ? Loop.Project(position, progress.Distance, ProjectionWindow)
            : Loop.Project(position);

        if (!progress.Tracked)
        {
            progress.Tracked = true;
            progress.Distance = distance;
            return;
        }

        var moved = Loop.Offset(progress.Distance, distance);

        if (moved > 0f)
        {
            // The gates it went through, in order round the lap.
            foreach (var gate in Enumerable.Range(0, Gates.Count).OrderBy(gate => Loop.Wrap(GateDistances[gate] - progress.Distance)))
            {
                if (Loop.Wrap(GateDistances[gate] - progress.Distance) > moved)
                    break;

                PassGate(progress, gate, now);
            }
        }

        progress.Distance = distance;
    }

    /// <summary>
    /// The kart went through a gate (its client said so, or it crossed it): the next one in order (a few may be
    /// missed) counts, and the finish counts a lap once enough of the lap's gates did.
    /// </summary>
    public void PassGate(RaceProgress progress, int gate, long now)
    {
        if (progress.Finished || gate < progress.NextGate || gate - progress.NextGate > 3)
            return;

        progress.NextGate = gate + 1;
        progress.GatesThisLap++;

        if (gate != FinishGate)
            return;

        // Every lap after the first leaves the first gate (on the start lane) out.
        var needed = (int)MathF.Ceiling((Gates.Count - (progress.Laps == 0 ? 0 : 1)) * GatesForALap);

        if (progress.GatesThisLap >= needed)
        {
            var lapTime = (now - progress.LapStartedAt) / 1000f;

            progress.Laps++;
            progress.LastLap = lapTime;
            progress.BestLap = progress.BestLap == 0f ? lapTime : MathF.Min(progress.BestLap, lapTime);
            progress.LapStartedAt = now;
        }

        progress.NextGate = 1;
        progress.GatesThisLap = 0;
    }

    /// <summary>
    /// The gate nearest a reported position, if one is close enough to be the one gone through.
    /// </summary>
    public int? GateAt(Vector3 position)
    {
        var gate = Enumerable.Range(0, Gates.Count).MinBy(gate => Vector3.Distance(Gates[gate], position));

        return Vector3.Distance(Gates[gate], position) < 30f ? gate : null;
    }

    /// <summary>
    /// How far round the race the kart is: its laps and where it is in the one it's on. Just past the finish it can
    /// still be found a little short of it, and the other way round.
    /// </summary>
    public float RaceDistance(RaceProgress progress)
    {
        var distance = progress.Distance;

        if (progress.NextGate <= 2 && distance > LapLength * 0.75f && progress.Laps > 0)
            distance -= LapLength;
        else if (progress.NextGate >= FinishGate && distance < LapLength * 0.25f)
            distance += LapLength;

        return progress.Laps * LapLength + distance;
    }

    #endregion

    #region Building

    private readonly record struct LinePoint(float Time, Vector3 Position);

    private static List<LinePoint> Points(List<float[]> line) =>
        line.Select(point => new LinePoint(point[0], new Vector3(point[1], point[2], point[3]))).ToList();

    /// <summary>
    /// Where a line comes nearest the finish gate, once round.
    /// </summary>
    private static int FinishIndex(List<LinePoint> line, Vector3 finish)
    {
        var start = line.FindIndex(point => point.Time > 20f);

        return Enumerable.Range(Math.Max(0, start), line.Count - Math.Max(0, start)).MinBy(index => Vector3.Distance(line[index].Position, finish));
    }

    /// <summary>
    /// Where a line's first lap passes nearest a point (the end of the main straight's stretch).
    /// </summary>
    private static int JoinIndex(List<LinePoint> line, int finish, Vector3 point)
    {
        var before = line.FindLastIndex(finish, candidate => candidate.Time < line[finish].Time - 10f);

        return Nearest(line, point, 0, Math.Max(1, before));
    }

    private static int Nearest(List<LinePoint> line, Vector3 point, int from, int to) =>
        Enumerable.Range(from, Math.Max(1, to - from + 1)).MinBy(index => Vector3.Distance(line[index].Position, point));

    /// <summary>
    /// A path through the points, each with the speed live drove on from it.
    /// </summary>
    private static RacePath Path(List<LinePoint> points, bool closed)
    {
        var speeds = new float[points.Count];

        for (var i = 0; i < points.Count; i++)
        {
            var (a, b) = i + 1 < points.Count ? (points[i], points[i + 1]) : (points[i - 1], points[i]);
            var seconds = b.Time - a.Time;

            // Joins between laps have no time between them: the speed carries over.
            speeds[i] = seconds > 0.05f ? Vector3.Distance(a.Position, b.Position) / seconds : i > 0 ? speeds[i - 1] : 20f;
        }

        return new RacePath(points.Select(point => point.Position).ToList(), speeds, closed);
    }

    #endregion
}

/// <summary>
/// An AI driver's way round: from its grid slot up the start lane (<see cref="Start"/>), then its lap line, joined at
/// <see cref="Entry"/>; the lap line's 0 is the finish.
/// </summary>
public sealed record DriverLine(RacePath Start, RacePath Loop, float Entry);

/// <summary>
/// How far a kart is round the race.
/// </summary>
public sealed class RaceProgress
{
    /// <summary>
    /// Where it is round the centre line (0 the finish).
    /// </summary>
    public float Distance { get; set; }

    public bool Tracked { get; set; }

    public int Laps { get; set; }

    /// <summary>
    /// The gate it goes through next.
    /// </summary>
    public int NextGate { get; set; }

    public int GatesThisLap { get; set; }

    public long LapStartedAt { get; set; }
    public float LastLap { get; set; }
    public float BestLap { get; set; }

    public bool Finished { get; set; }
    public long FinishedAt { get; set; }

    /// <summary>
    /// Its place when it finished; the order stays.
    /// </summary>
    public int FinishPlace { get; set; } = -1;
}
