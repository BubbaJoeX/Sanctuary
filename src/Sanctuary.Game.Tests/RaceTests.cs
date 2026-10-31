using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.VehicleGames;
using Sanctuary.Game.VehicleGames.Race;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public class RaceTests
{
    private const ulong CharacterGuid = 0x4B52CC6A9573CA91;

    // Live: the player's distance to the finish ran on without a jump through their first lap's end, from 3626.5 at
    // 108.16 to 3587.3 at 109.17, two laps' worth: a lap of about 1805 m.
    private const float LiveLapLength = 1805f;

    [TestMethod]
    public void Track_IsTheLiveLap()
    {
        var track = new RaceTrack(Track());

        Assert.AreEqual(LiveLapLength, track.LapLength, LiveLapLength * 0.03f);

        // The gates come round the lap in order (the first is on the start lane, passed on the first lap only), the
        // last at the finish.
        for (var gate = 2; gate < track.Gates.Count; gate++)
            Assert.IsTrue(track.GateDistances[gate] > track.GateDistances[gate - 1] || gate == track.FinishGate && track.GateDistances[gate] < 30f,
                $"Gate {gate + 1} at {track.GateDistances[gate]} isn't past gate {gate} at {track.GateDistances[gate - 1]}.");

        // Every AI driver has a way round (live, one of them cut the corner between gates 18 and 19).
        foreach (var driver in Track().Drivers)
        {
            Assert.IsTrue(track.TryGetLine(driver.Slot, out var line));
            Assert.AreEqual(LiveLapLength, line.Loop.Length, LiveLapLength * 0.1f, $"Slot {driver.Slot}'s lap.");
            Assert.IsTrue(line.Start.Length > 100f, $"Slot {driver.Slot}'s start lane.");
        }
    }

    /// <summary>
    /// The live race replayed through the server's progress: the player's kart by its own states, the AI karts by
    /// their live lines. Laps, places and distances must come out as the live server's did.
    /// </summary>
    [TestMethod]
    public void Progress_ReplaysTheLiveRace()
    {
        var track = new RaceTrack(Track());
        var replay = VehicleGamePacketTests.Document.RootElement.GetProperty("race_replay");

        var lineStart = replay.GetProperty("line_start").GetSingle();
        var player = replay.GetProperty("player").EnumerateArray().Select(point => point.EnumerateArray().Select(value => value.GetSingle()).ToArray()).ToList();
        var slots = replay.GetProperty("slots").EnumerateObject().ToDictionary(slot => slot.Name, slot => slot.Value.GetInt32());

        var karts = slots.ToDictionary(slot => slot.Key, _ => new RaceProgress());
        var lines = Track().Drivers.ToDictionary(driver => driver.Slot, driver => driver.Line);

        var live = replay.GetProperty("progress").EnumerateObject().ToDictionary(
            kart => kart.Name,
            kart => kart.Value.EnumerateArray().Select(sample => sample.EnumerateArray().Select(value => value.GetSingle()).ToArray()).ToList());

        var samples = 0;
        var placesRight = 0;
        var placesClose = 0;

        for (var tick = 0; tick * 0.25f <= player[^1][0]; tick++)
        {
            var seconds = tick * 0.25f;
            var now = (long)(seconds * 1000f);

            foreach (var (guid, progress) in karts)
            {
                Vector3? position = slots[guid] == 0 ? At(player, seconds) : At(lines[slots[guid]], seconds - lineStart);

                if (position is null)
                    continue;

                if (!progress.Tracked)
                    progress.LapStartedAt = 0;

                track.Advance(progress, position.Value, now);
            }

            // The live progress samples in this tick (once the AI lines start): places and laps as the server had them.
            foreach (var sample in live[CharacterGuid.ToString("x").Insert(0, "0x")].Where(sample => sample[0] >= seconds && sample[0] < seconds + 0.25f && sample[0] > 1f))
            {
                var mine = karts[CharacterGuid.ToString("x").Insert(0, "0x")];
                var liveLaps = (int)sample[3];

                // Laps exactly, away from the moment the line is crossed.
                if (MathF.Abs(sample[0] - 56.7f) > 1f)
                    Assert.AreEqual(liveLaps, mine.Laps, $"Laps at {sample[0]} s.");

                // The distance to the finish, three laps from GO, measured round the track as live did (live's grid
                // was a little behind its finish line, the server's a little past it).
                if (sample[2] > 0f && sample[0] > 3f)
                {
                    var distance = 3 * track.LapLength - track.RaceDistance(mine);
                    Assert.AreEqual(sample[2] / LiveLapLength, distance / track.LapLength, 0.05f, $"Laps left at {sample[0]} s.");
                }

                // The player's place among the karts live had there.
                var place = karts.Count(kart => kart.Key != CharacterGuid.ToString("x").Insert(0, "0x") && track.RaceDistance(kart.Value) > track.RaceDistance(mine));

                samples++;

                if (place == (int)sample[1])
                    placesRight++;

                if (Math.Abs(place - (int)sample[1]) <= 1)
                    placesClose++;
            }
        }

        // Karts a few metres apart on their own lines swap places now and then.
        Assert.IsTrue(samples > 40, $"Only {samples} live samples compared.");
        Assert.IsTrue(placesRight >= samples * 0.6f, $"The player's place matched live in {placesRight} of {samples} samples.");
        Assert.IsTrue(placesClose >= samples * 0.85f, $"The player's place was within one of live's in {placesClose} of {samples} samples.");
        Assert.AreEqual(1, karts[CharacterGuid.ToString("x").Insert(0, "0x")].Laps, "The live player finished one lap.");
    }

    /// <summary>
    /// A whole race of the seven live AI drivers: each goes round three times in about the live lap time, on the
    /// track, and they finish in order.
    /// </summary>
    [TestMethod]
    public void AiKarts_RaceThreeLaps()
    {
        var definition = Track();
        var track = new RaceTrack(definition);
        var simulation = new RaceSimulation(track, definition.Laps, new Random(28));

        var finished = new List<RaceKart>();
        var pickups = 0;

        simulation.Finished = finished.Add;
        simulation.PickupTaken = (_, _, _) => pickups++;

        foreach (var driver in definition.Drivers)
        {
            var slot = definition.Grid.Single(slot => slot.Slot == driver.Slot);

            Assert.IsTrue(track.TryGetLine(driver.Slot, out var line));

            simulation.Add(new RaceKart((ulong)driver.Slot, driver.Slot, new VehicleConfig([]), slot.Point, slot.Heading) { Line = line, Pace = 0.98f });
        }

        const int tickMs = 50;

        simulation.Start(1);

        for (long now = 1; now < 300_000 && finished.Count < simulation.Karts.Count; now += tickMs)
        {
            simulation.Update(now, tickMs / 1000f);

            foreach (var kart in simulation.Karts)
            {
                Assert.IsFalse(float.IsNaN(kart.Position.X), $"Slot {kart.Slot} at {now} ms.");

                var onTrack = track.Loop.PositionAt(track.Loop.Project(kart.Position));
                Assert.IsTrue(Vector3.Distance(onTrack, kart.Position) < 100f || kart.OnStartLane, $"Slot {kart.Slot} off the track at {kart.Position}.");
            }
        }

        Assert.AreEqual(simulation.Karts.Count, finished.Count, "Not every kart finished.");

        foreach (var kart in finished)
        {
            Assert.AreEqual(3, kart.Progress.Laps);
            Assert.IsTrue(kart.Progress.BestLap is > 45f and < 70f, $"Slot {kart.Slot}'s best lap was {kart.Progress.BestLap} s.");
        }

        CollectionAssert.AreEqual(finished, simulation.Standings(), "The standings are the finishing order.");
        Assert.IsTrue(pickups > 10, $"Only {pickups} boxes taken.");
    }

    [TestMethod]
    public void Gates_CountALapOnlyRoundTheTrack()
    {
        var track = new RaceTrack(Track());
        var progress = new RaceProgress();

        // Straight to the finish gate: no lap.
        track.PassGate(progress, track.FinishGate, 1000);
        Assert.AreEqual(0, progress.Laps);

        // Every gate in order, the start lane's first: a lap, timed.
        progress = new RaceProgress { LapStartedAt = 0 };

        for (var gate = 0; gate < track.Gates.Count; gate++)
            track.PassGate(progress, gate, 56_384);

        Assert.AreEqual(1, progress.Laps);
        Assert.AreEqual(56.384f, progress.LastLap, 0.001f);
        Assert.AreEqual(1, progress.NextGate, "The next lap leaves the start lane's gate out.");

        // A missed gate or two still counts; a gate passed again doesn't.
        for (var gate = 1; gate < track.Gates.Count; gate++)
        {
            if (gate is 5 or 12)
                continue;

            track.PassGate(progress, gate, 110_000);
            track.PassGate(progress, gate, 110_000);
        }

        Assert.AreEqual(2, progress.Laps);
        Assert.AreEqual(53.616f, progress.LastLap, 0.001f);
        Assert.AreEqual(53.616f, progress.BestLap, 0.001f);
    }

    [TestMethod]
    public void Offer_MatchesTheLiveRace()
    {
        var games = DerbyTests.LoadGames();

        Assert.IsTrue(games.TryGetStage(28, out var stage));

        var participant = new EncounterParticipant
        {
            Key = unchecked((int)0xDE2106FB),
            Guid = CharacterGuid,
            Position = stage.EntryPosition,
            Name = "Maÿhem",
            ServerId = 1008
        };

        var live = VehicleGamePacketTests.Live("race", "S>C 41/114");

        CollectionAssert.AreEqual(live[0], VehicleGameManager.BuildDetails(0x000E003E, stage, [participant], isUpdate: false).Serialize(), "The offer");

        participant.Entered = true;

        CollectionAssert.AreEqual(live[1], VehicleGameManager.BuildDetails(0x000E003E, stage, [participant], isUpdate: true).Serialize(), "The update once in");
    }

    [TestMethod]
    public void SetRaceConfig_IsTheLiveTrack()
    {
        var definition = Track();
        var live = VehicleGamePacketTests.Live("race", "S>C 34")[0];

        Assert.IsTrue(VehicleRacePacketSetRaceConfig.TryDeserialize(live, out var config));
        Assert.AreEqual((uint)definition.Laps, config.Laps);
        Assert.AreEqual((uint)definition.TrackId, config.TrackId);
        Assert.AreEqual(definition.FinishCameraDelay, config.FinishCameraDelay);
        Assert.AreEqual((uint)definition.MissileExplosionEffectId, config.MissileExplosionEffectId);
        Assert.AreEqual((uint)definition.MineExplosionEffectId, config.MineExplosionEffectId);

        for (var i = 0; i < 16; i++)
            Assert.AreEqual(definition.FinishCamera[i], config.FinishCameraTransform[i / 4, i % 4], 0.001f);
    }

    private static Vector3? At(List<float[]> points, float seconds)
    {
        if (seconds < points[0][0] || seconds > points[^1][0])
            return null;

        var next = points.FindIndex(point => point[0] >= seconds);

        if (next <= 0)
            return new Vector3(points[0][1], points[0][2], points[0][3]);

        var a = points[next - 1];
        var b = points[next];
        var t = (seconds - a[0]) / Math.Max(1e-3f, b[0] - a[0]);

        return Vector3.Lerp(new Vector3(a[1], a[2], a[3]), new Vector3(b[1], b[2], b[3]), t);
    }

    private static RaceTrackDefinition Track() => DerbyTests.LoadGames().Definition.Tracks.Single(track => track.Zone == "sg_kt_track");
}
