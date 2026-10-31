using System;
using System.Linq;
using System.Numerics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.VehicleGames;
using Sanctuary.Game.VehicleGames.Soccer;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public class SoccerTests
{
    private const ulong CharacterGuid = 0x4B52CC6A9573CA91;

    [TestMethod]
    public void Setup_IsTheLiveMatchs()
    {
        var pitch = Pitch();

        var config = new SoccerPacketSetClientConfig
        {
            PitchBounds = new Vector4(pitch.PitchBounds[0], pitch.PitchBounds[1], pitch.PitchBounds[2], pitch.PitchBounds[3]),
            Tuning = [.. pitch.Tuning],
            Tuning2 = [.. pitch.Tuning2],
            BallModel = pitch.BallModel,
            Unknown = pitch.Unknown,
            Unknown2 = pitch.Unknown2,
            BallSpawn = new Vector4(pitch.BallSpawn[0], pitch.BallSpawn[1], pitch.BallSpawn[2], 1f),
            Tuning3 = [.. pitch.Tuning3],
            Unknown3 = pitch.Unknown3,
            Unknown4 = pitch.Unknown4,
            Unknown5 = pitch.Unknown5
        };

        CollectionAssert.AreEqual(VehicleGamePacketTests.Live("soccer", "S>C 7")[0], config.Serialize(), "SetClientConfig");

        var colours = new SoccerPacketTeamColours
        {
            Tint = pitch.Teams[0].Tint,
            Tint2 = pitch.Teams[1].Tint,
            Colour = pitch.Teams[0].Colour,
            Colour2 = pitch.Teams[1].Colour,
            NameId = (uint)pitch.Teams[0].NameId,
            NameId2 = (uint)pitch.Teams[1].NameId
        };

        CollectionAssert.AreEqual(VehicleGamePacketTests.Live("soccer", "S>C 8")[0], colours.Serialize(), "TeamColours");

        // The tuning live gave each player, as the client got it.
        var tuning = VehicleGamePacketTests.Live("soccer", "S>C 21")[0];

        Assert.IsTrue(SoccerPacketSetSoccerPlayerOneTimeStats.TryDeserialize(tuning, out var oneTime));

        var hex = oneTime.Guid == CharacterGuid ? pitch.PlayerOneTimeStats
            : pitch.Players.Select(player => player.OneTimeStats).First(stats => stats == Convert.ToHexString(oneTime.Stats).ToLowerInvariant());

        CollectionAssert.AreEqual(oneTime.Stats, Convert.FromHexString(hex));

        // Three a side and a goalie each.
        Assert.AreEqual(7, pitch.Players.Count);
        Assert.AreEqual(3, pitch.Players.Count(player => player.Team == 0));
        Assert.AreEqual(1, pitch.Players.Count(player => player.Team == 0 && player.Position == 3));
        Assert.AreEqual(1, pitch.Players.Count(player => player.Team == 1 && player.Position == 3));
    }

    [TestMethod]
    public void Offer_MatchesTheLiveMatch()
    {
        var games = DerbyTests.LoadGames();

        Assert.IsTrue(games.TryGetStage(61, out var stage));

        var participant = new EncounterParticipant
        {
            Key = unchecked((int)0xDE2106FB),
            Guid = CharacterGuid,
            Position = stage.EntryPosition,
            Name = "Maÿhem",
            ServerId = 1008
        };

        var live = VehicleGamePacketTests.Live("soccer", "S>C 41/114");

        // The live offer stood the player where they were in Sacred Grove; the server puts them on their spot.
        Assert.IsTrue(EncounterDetailsResponsePacketOffer(live[0], out var livePosition));
        participant.Position = livePosition;

        CollectionAssert.AreEqual(live[0], VehicleGameManager.BuildDetails(0x00070048, stage, [participant], isUpdate: false).Serialize(), "The offer");

        participant.Entered = true;

        CollectionAssert.AreEqual(live[1], VehicleGameManager.BuildDetails(0x00070048, stage, [participant], isUpdate: true).Serialize(), "The update once in");
    }

    /// <summary>
    /// The live kick at t+59.06: the ball's next update, 0.15 s on.
    /// </summary>
    [TestMethod]
    public void Ball_FliesAsTheLiveBallDid()
    {
        var pitch = Pitch();
        var bounds = new Vector4(pitch.PitchBounds[0], pitch.PitchBounds[1], pitch.PitchBounds[2], pitch.PitchBounds[3]);

        var position = new Vector3(242.13f, 2.37f, 209.35f);
        var velocity = new Vector3(15.08f, -2.19f, 7.56f);

        for (var i = 0; i < 3; i++)
            (position, velocity, _) = SoccerRules.Step(position, velocity, 0.05f, bounds, pitch.BallSpawn[2]);

        Assert.AreEqual(244.38f, position.X, 0.1f);
        Assert.AreEqual(1.91f, position.Y, 0.1f);
        Assert.AreEqual(210.48f, position.Z, 0.1f);
        Assert.AreEqual(14.58f, velocity.X, 0.1f);
        Assert.AreEqual(-3.61f, velocity.Y, 0.1f);
        Assert.AreEqual(7.31f, velocity.Z, 0.1f);

        // The live goal: over the high x goal line in the mouth, team 0's goal, so team 1 scored.
        Assert.AreEqual(1, SoccerRules.GoalAt(new Vector3(253.81f, 0.13f, 212.67f), bounds, pitch.BallSpawn[2]));
        Assert.AreEqual(1, 1 - SoccerRules.Defender(1));
        Assert.IsNull(SoccerRules.GoalAt(new Vector3(253.81f, 0.13f, 220f), bounds, pitch.BallSpawn[2]), "Wide of the post.");
        Assert.IsNull(SoccerRules.GoalAt(new Vector3(240f, 0.13f, 212.67f), bounds, pitch.BallSpawn[2]));
    }

    [TestMethod]
    public void Stick_RunsTheWayLiveAiRan()
    {
        // Live: stick (1, 0) ran an AI player up x, stick (0, -1) up z.
        Assert.AreEqual(Vector3.UnitX, SoccerRules.StickDirection(1f, 0f));
        Assert.AreEqual(Vector3.UnitZ, SoccerRules.StickDirection(0f, -1f));

        var (x, y, _) = SoccerRules.Stick(new Vector3(244.3f, 0f, 203.9f), new Vector3(244.9f, 0f, 209.9f));

        Assert.AreEqual((0f, -1f), (x, y));
    }

    /// <summary>
    /// Two halves of eight AI players: the ball stays on the pitch, changes hands, and goals go in.
    /// </summary>
    [TestMethod]
    public void AiPlayers_PlayAMatch()
    {
        var pitch = Pitch();
        var bounds = new Vector4(pitch.PitchBounds[0], pitch.PitchBounds[1], pitch.PitchBounds[2], pitch.PitchBounds[3]);
        var centre = new Vector3(pitch.BallSpawn[0], SoccerRules.BallRadius, pitch.BallSpawn[2]);

        var simulation = new SoccerSimulation(bounds, centre, pitch.PickupSlots.Select(slot => new Vector2(slot[0], slot[1])).ToList(), new Random(61));

        // The player's spot gets an AI player too.
        simulation.Add(new SoccerPlayer(1, 0, 0, new Vector3(pitch.PlayerKickOff[0], 0f, pitch.PlayerKickOff[2])));

        foreach (var (definition, index) in pitch.Players.Select((definition, index) => (definition, index)))
            simulation.Add(new SoccerPlayer((ulong)(index + 2), definition.Team, definition.Position, new Vector3(definition.KickOff[0], 0f, definition.KickOff[2])));

        var goals = 0;
        var changes = 0;
        var steals = 0;
        var pickups = 0;

        simulation.Scored = (_, _, _) => goals++;
        simulation.Acquired = (_, previous) => { changes++; if (previous != 0) steals++; };
        simulation.PickupTaken = (_, _, _) => pickups++;

        simulation.ResetFormation();

        const int tickMs = 50;
        long now = 1;

        for (var tick = 0; tick < 2 * 180 * 1000 / tickMs; tick++, now += tickMs)
        {
            simulation.Update(now, tickMs / 1000f);

            if (!simulation.Ball.InPlay)
                simulation.ResetFormation();

            var ball = simulation.Ball.Position;

            Assert.IsFalse(float.IsNaN(ball.X) || float.IsNaN(ball.Y) || float.IsNaN(ball.Z), $"The ball at {now} ms.");
            Assert.IsTrue(ball.Z >= bounds.Y - 0.01f && ball.Z <= bounds.W + 0.01f, $"The ball off the side at {ball}.");
            Assert.IsTrue(ball.Y >= SoccerRules.BallRadius - 0.01f, $"The ball under the pitch at {ball}.");

            foreach (var player in simulation.Players)
                Assert.IsTrue(player.Location.Z >= bounds.Y - 0.01f && player.Location.Z <= bounds.W + 0.01f, $"Player {player.Guid} off the pitch at {player.Location}.");
        }

        Assert.IsTrue(goals >= 2, $"Only {goals} goals.");
        Assert.AreEqual(goals, simulation.Score.Sum());
        Assert.IsTrue(changes > 20, $"The ball changed hands only {changes} times.");
        Assert.IsTrue(steals > 3, $"Only {steals} steals.");
        Assert.IsTrue(pickups > 0, "Nobody took a power-up.");
        Assert.AreEqual(goals, simulation.Players.Sum(player => player.Goals) + OwnGoals(simulation));
    }

    [TestMethod]
    public void Payout_PaysTheWinners()
    {
        var stage = new VehicleGameStageDefinition { Kind = VehicleGameKind.Soccer, Stars = 120, Coins = 20 };

        Assert.AreEqual((120, 20), ArenaRules.Payout(stage, 0, won: true));
        Assert.AreEqual((72, 10), ArenaRules.Payout(stage, 0, won: false), "A draw.");
        Assert.AreEqual((48, 0), ArenaRules.Payout(stage, 1, won: false));
    }

    private static int OwnGoals(SoccerSimulation simulation) => simulation.Score.Sum() - simulation.Players.Sum(player => player.Goals);

    private static bool EncounterDetailsResponsePacketOffer(byte[] live, out Vector4 position)
    {
        // The participant's position: after the header (4), encounter id and reply id (8), details id, unknown,
        // team count, member count (16), key, guid, five bools and an int.
        var offset = 4 + 8 + 16 + 4 + 8 + 1 + 4 + 1 + 1 + 4;

        position = new Vector4(BitConverter.ToSingle(live, offset), BitConverter.ToSingle(live, offset + 4), BitConverter.ToSingle(live, offset + 8), BitConverter.ToSingle(live, offset + 12));

        return true;
    }

    private static SoccerPitchDefinition Pitch() => DerbyTests.LoadGames().Definition.Soccer;
}
