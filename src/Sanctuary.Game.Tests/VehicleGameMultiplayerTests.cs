using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Core.IO;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Groups;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.VehicleGames;
using Sanctuary.Game.VehicleGames.Derby;
using Sanctuary.Game.VehicleGames.Race;
using Sanctuary.Game.VehicleGames.Soccer;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

/// <summary>
/// Several players in one race, derby or soccer match: the start held for everyone coming, every game state and
/// countdown number waiting on every player, the players' own states going on to the others, crashes both clients
/// report counting once, the soccer lineup and first-come ball, leaving, and the lobby's matchmaking queues.
/// </summary>
[TestClass]
public class VehicleGameMultiplayerTests
{
    private const ulong Alice = 0x4B52CC6A9573CA91;
    private const ulong Bob = 0x4B52CC6A9573CA92;
    private const ulong Cleo = 0x4B52CC6A9573CA93;
    private const ulong Dan = 0x4B52CC6A9573CA94;

    #region The handshake

    [TestMethod]
    public void Setup_WaitsForEveryoneComing()
    {
        var handshake = new ArenaHandshake();

        Assert.IsFalse(handshake.ReadyToSetUp([Alice, Bob], 0), "Nobody has registered.");

        Assert.IsTrue(handshake.Register(Alice, 111, 1000));
        Assert.IsFalse(handshake.ReadyToSetUp([Alice, Bob], 2000), "Bob is still coming.");

        Assert.IsTrue(handshake.Register(Bob, 222, 9000));
        Assert.IsTrue(handshake.ReadyToSetUp([Alice, Bob], 9000));

        Assert.AreEqual(111UL, handshake.ClockOf(Alice));
        Assert.AreEqual(222UL, handshake.ClockOf(Bob));
    }

    [TestMethod]
    public void Setup_GoesWithoutLatecomersAfterTheJoinWait()
    {
        var handshake = new ArenaHandshake();

        handshake.Register(Alice, 1, 1000);

        Assert.IsFalse(handshake.ReadyToSetUp([Alice, Bob], 1000 + ArenaMatch.JoinWaitMs));
        Assert.IsTrue(handshake.ReadyToSetUp([Alice, Bob], 1001 + ArenaMatch.JoinWaitMs));

        // Once it's set up, a late client's register is turned down: there's no vehicle for it.
        handshake.Seat(Alice);
        handshake.SetGameState(3, 50_000);

        Assert.IsFalse(handshake.Register(Bob, 2, 50_100));
        Assert.IsFalse(handshake.IsSeated(Bob));
    }

    [TestMethod]
    public void GameStates_WaitForEveryPlayersAck()
    {
        var handshake = Seated(Alice, Bob);

        handshake.SetGameState(5, 10_000);

        handshake.Acknowledge(Alice, 5);
        Assert.IsFalse(handshake.Acknowledged(10_100), "Bob hasn't acknowledged.");

        // An old state's ack, or one from a player with no vehicle, counts for nothing.
        handshake.Acknowledge(Bob, 4);
        handshake.Acknowledge(Cleo, 5);
        Assert.IsFalse(handshake.Acknowledged(10_200));

        handshake.Acknowledge(Bob, 5);
        Assert.IsTrue(handshake.Acknowledged(10_300));

        // The next state starts over.
        handshake.SetGameState(6, 10_400);
        Assert.IsFalse(handshake.Acknowledged(10_500));
    }

    [TestMethod]
    public void GameStates_GoOnWithoutAClientThatStoppedAnswering()
    {
        var handshake = Seated(Alice, Bob);

        handshake.SetGameState(4, 0);
        handshake.Acknowledge(Alice, 4);

        Assert.IsFalse(handshake.Acknowledged(ArenaMatch.AckTimeoutMs));
        Assert.IsTrue(handshake.Acknowledged(ArenaMatch.AckTimeoutMs + 1));
    }

    [TestMethod]
    public void Leaving_IsWaitedForNoMore()
    {
        var handshake = Seated(Alice, Bob);

        handshake.SetGameState(7, 0);
        handshake.Acknowledge(Alice, 7);

        handshake.Leave(Bob, 100);

        Assert.IsTrue(handshake.Acknowledged(100));
        Assert.IsFalse(handshake.IsSeated(Bob));
        Assert.IsFalse(handshake.IsRegistered(Bob));
        CollectionAssert.AreEquivalent(new[] { Alice }, handshake.Seated.ToList());
    }

    [TestMethod]
    public void Countdown_WaitsForEveryPlayersEcho()
    {
        var handshake = Seated(Alice, Bob);

        handshake.SetGameState(8, 0);
        handshake.StartCountdown(4, 0);

        // The 4 is never echoed live: the 3 goes after the first wait.
        Assert.IsFalse(handshake.CountdownDue(349, firstMs: 350));
        Assert.IsTrue(handshake.CountdownDue(350, firstMs: 350));

        handshake.StartCountdown(3, 400);
        handshake.Echo(Alice, 3, 450);
        Assert.AreEqual(0L, handshake.CountdownEchoedAt, "Bob hasn't echoed.");
        Assert.IsFalse(handshake.CountdownDue(1000, firstMs: 350));

        // An echo of another number is no echo.
        handshake.Echo(Bob, 4, 500);
        Assert.AreEqual(0L, handshake.CountdownEchoedAt);

        handshake.Echo(Bob, 3, 600);
        Assert.AreEqual(600L, handshake.CountdownEchoedAt);
        Assert.IsFalse(handshake.CountdownDue(899, firstMs: 350));
        Assert.IsTrue(handshake.CountdownDue(900, firstMs: 350));

        // A player who leaves before echoing lets the others' echoes count.
        handshake.StartCountdown(2, 1000);
        handshake.Echo(Alice, 2, 1100);
        handshake.Leave(Bob, 1200);
        Assert.AreEqual(1200L, handshake.CountdownEchoedAt);

        // Nobody echoing at all: the next number after 1.5 s anyway.
        handshake.StartCountdown(1, 2000);
        Assert.IsTrue(handshake.CountdownDue(3500, firstMs: 350));
    }

    private static ArenaHandshake Seated(params ulong[] players)
    {
        var handshake = new ArenaHandshake();

        foreach (var guid in players)
        {
            handshake.Register(guid, 0, 0);
            handshake.Seat(guid);
        }

        return handshake;
    }

    #endregion

    #region Relays and crashes

    /// <summary>
    /// A player's kart state goes on to the others as the server's kart state, the same 89 bytes, addressed by the
    /// kart's grid slot.
    /// </summary>
    [TestMethod]
    public void RaceRelay_IsTheClientsStateBySlot()
    {
        foreach (var live in VehicleGamePacketTests.Live("race", "C>S 6"))
        {
            Assert.IsTrue(VehicleRacePacketUpdateVehicleRaceServerData.TryDeserialize(live, out var state));

            for (var slot = 0; slot < 8; slot++)
            {
                var relayed = RaceMatch.Relay(state, slot).Serialize();

                Assert.AreEqual(live.Length, relayed.Length);
                Assert.AreEqual(5, BitConverter.ToUInt16(relayed, 2), "Relayed as sub 5.");

                // The flags byte comes just before the clock (u64) that ends the packet.
                var flagsAt = live.Length - 9;

                for (var i = 4; i < live.Length; i++)
                {
                    if (i == flagsAt)
                    {
                        Assert.AreEqual(slot, (relayed[i] >> 3) & 7, "The slot rides in bits 3-5.");
                        Assert.AreEqual(live[i] & ~0x38, relayed[i] & ~0x38, "The other flag bits stay.");
                    }
                    else
                        Assert.AreEqual(live[i], relayed[i], $"Byte {i} changed.");
                }
            }
        }
    }

    [TestMethod]
    public void DerbyCrash_BothClientsReportCountsOnce()
    {
        var games = DerbyTests.LoadGames();
        var simulation = new DerbySimulation(games.Definition.Derby, (float x, float z, float fromY, float maxDrop, out float height) => { height = 0f; return true; }, new Random(39));

        var hits = 0;
        simulation.Crashed = (_, _, _) => hits++;

        var alice = new DerbyCar(Alice, new VehicleConfig([]), Vector3.Zero, 0f) { Participant = Participant(Alice) };
        var bob = new DerbyCar(Bob, new VehicleConfig([]), new Vector3(0f, 0f, 3f), MathF.PI) { Participant = Participant(Bob) };

        simulation.Add(alice);
        simulation.Add(bob);

        // Head on, both at 10 m/s.
        alice.Velocity = new Vector3(0f, 0f, 10f);
        bob.Velocity = new Vector3(0f, 0f, -10f);

        Assert.IsTrue(simulation.TryCrash(alice, bob, 1000), "Alice's client reports it.");
        Assert.IsFalse(simulation.TryCrash(bob, alice, 1050), "Bob's client reports the same crash.");
        Assert.AreEqual(1, hits);

        var health = (alice.Health, bob.Health);

        Assert.IsFalse(simulation.TryCrash(alice, bob, 1000 + DerbyRules.CrashCooldownMs - 1));
        Assert.AreEqual(health, (alice.Health, bob.Health), "No damage twice for one crash.");

        Assert.IsTrue(simulation.TryCrash(bob, alice, 1000 + DerbyRules.CrashCooldownMs), "A crash later is a new one.");
        Assert.AreEqual(2, hits);
    }

    [TestMethod]
    public void RaceBump_BetweenPlayersGoesToTheOtherOnce()
    {
        var definition = DerbyTests.LoadGames().Definition.Tracks.Single(track => track.Zone == "sg_kt_track");
        var simulation = new RaceSimulation(new RaceTrack(definition), definition.Laps, new Random(28));

        var bumps = new List<(ulong From, ulong To, float Impulse)>();
        simulation.PlayersBumped = (kart, other, impulse) => bumps.Add((kart.Guid, other.Guid, impulse));

        var slots = definition.Grid;
        var alice = new RaceKart(Alice, slots[0].Slot, new VehicleConfig([]), slots[0].Point, slots[0].Heading) { Participant = Participant(Alice) };
        var bob = new RaceKart(Bob, slots[1].Slot, new VehicleConfig([]), slots[1].Point, slots[1].Heading) { Participant = Participant(Bob) };

        simulation.Add(alice);
        simulation.Add(bob);

        simulation.OnReportedBump(alice, bob, 900f, 5000);
        simulation.OnReportedBump(bob, alice, 880f, 5100);

        Assert.AreEqual(1, bumps.Count, "Both clients' reports are one bump.");
        Assert.AreEqual((Alice, Bob, 900f), bumps[0], "Bob's client is told, with Alice's push.");

        simulation.OnReportedBump(bob, alice, 700f, 7000);

        Assert.AreEqual(2, bumps.Count);
        Assert.AreEqual((Bob, Alice, 700f), bumps[1]);
    }

    #endregion

    #region Soccer

    [TestMethod]
    public void SoccerLineup_KeepsAGroupTogetherAndTheGoalLast()
    {
        // The live player's place first, the field, then the goal; the fifth of a group plays for the others.
        var group = SoccerRules.Lineup([0, 0, 0, 0, 0]);

        CollectionAssert.AreEqual(new (int, int)?[] { (0, 0), (0, 1), (0, 2), (0, 3), (1, 0) }, group);

        // Players queued apart are spread over both sides.
        var spread = SoccerRules.Lineup([0, 1, 0, 1]);

        CollectionAssert.AreEqual(new (int, int)?[] { (0, 0), (1, 0), (0, 1), (1, 1) }, spread);

        // Eight places in all.
        var crowd = SoccerRules.Lineup(Enumerable.Repeat(0, 9).ToList());

        Assert.AreEqual(8, crowd.Count(place => place is not null));
        Assert.IsNull(crowd[8]);
    }

    [TestMethod]
    public void SoccerLineup_LeavesEachHumansPlaceToNoAi()
    {
        var pitch = DerbyTests.LoadGames().Definition.Soccer;

        foreach (var humans in new[] { 1, 2, 4, 6, 8 })
        {
            var lineup = SoccerRules.Lineup(Enumerable.Range(0, humans).Select(index => index % 2).ToList()).OfType<(int Team, int Position)>().ToList();
            var ai = pitch.Players.Where(definition => !lineup.Contains((definition.Team, definition.Position))).ToList();

            Assert.AreEqual(humans, lineup.Distinct().Count(), "One place each.");
            Assert.AreEqual(8, humans + ai.Count, $"{humans} players and {ai.Count} AI fill the pitch.");
        }
    }

    [TestMethod]
    public void SoccerBall_GoesToWhoeverReachesItFirst()
    {
        var pitch = DerbyTests.LoadGames().Definition.Soccer;
        var bounds = new Vector4(pitch.PitchBounds[0], pitch.PitchBounds[1], pitch.PitchBounds[2], pitch.PitchBounds[3]);
        var centre = new Vector3(pitch.BallSpawn[0], SoccerRules.BallRadius, pitch.BallSpawn[2]);

        var simulation = new SoccerSimulation(bounds, centre, [], new Random(61));

        // Two players on opposite teams run at the loose ball; Bob's client has him a little nearer.
        var alice = new SoccerPlayer(Alice, 0, 0, centre + new Vector3(3f, 0f, 0f)) { Participant = Participant(Alice) };
        var bob = new SoccerPlayer(Bob, 1, 0, centre - new Vector3(3f, 0f, 0f)) { Participant = Participant(Bob) };

        simulation.Add(alice);
        simulation.Add(bob);
        simulation.ResetFormation();

        var holders = new List<ulong>();
        simulation.Acquired = (holder, _) => holders.Add(holder.Guid);

        alice.Location = centre with { Y = 0f } + new Vector3(0.9f, 0f, 0f);
        bob.Location = centre with { Y = 0f } - new Vector3(0.6f, 0f, 0f);

        simulation.Update(1000, 0.05f);

        CollectionAssert.AreEqual(new[] { Bob }, holders, "One of them has it: the nearer.");
        Assert.AreEqual(Bob, simulation.Ball.Holder);

        // Alice right on top of it now takes nothing without a tackle.
        alice.Location = bob.Location;
        simulation.Update(1050, 0.05f);

        Assert.AreEqual(Bob, simulation.Ball.Holder);
        Assert.AreEqual(1, holders.Count);

        // A team mate's tackle takes nothing either.
        var cleo = new SoccerPlayer(Cleo, 1, 1, bob.Location) { Participant = Participant(Cleo) };
        simulation.Add(cleo);

        simulation.OnPlayerState(cleo, 36, 0, Vector3.UnitX, 1100);
        Assert.AreEqual(Bob, simulation.Ball.Holder);

        // A player who leaves with the ball lets it go.
        simulation.Remove(bob);
        Assert.IsTrue(simulation.Ball.IsLoose);
    }

    #endregion

    #region The queues

    [TestMethod]
    public void Queue_MatchesAsSoonAsAGameIsFull()
    {
        var queue = new VehicleGameQueue(Queue(maxPlayers: 2));

        queue.Add(Request(1, 0, Alice));
        Assert.IsNull(queue.TryMatch(1000, 77, 30));

        queue.Add(Request(2, 2000, Bob));

        var match = queue.TryMatch(2000, 77, 30);

        Assert.IsNotNull(match);
        Assert.AreEqual(77UL, match.InvitationId);
        CollectionAssert.AreEqual(new[] { Alice, Bob }, match.Players.ToList());
        Assert.AreEqual(0, queue.Requests.Count);
        Assert.IsTrue(queue.Holds(Alice), "Matched, still the queue's until they answer.");
    }

    [TestMethod]
    public void Queue_StartsWithWhoeverIsThereAfterTheWait()
    {
        var queue = new VehicleGameQueue(Queue(maxPlayers: 5));

        queue.Add(Request(1, 0, Alice));

        Assert.IsNull(queue.TryMatch(29_999, 1, 30));

        var match = queue.TryMatch(30_000, 1, 30);

        Assert.IsNotNull(match, "The AI take the places left.");
        CollectionAssert.AreEqual(new[] { Alice }, match.Players.ToList());
        Assert.AreEqual(30, queue.AverageWaitSeconds);
    }

    [TestMethod]
    public void Queue_KeepsAGroupTogether()
    {
        var queue = new VehicleGameQueue(Queue(maxPlayers: 3));

        queue.Add(Request(1, 0, Cleo));
        queue.Add(Request(2, 100, Alice, Bob, Dan));
        queue.Add(Request(3, 200, Bob + 10, Bob + 11));

        // Cleo and the group of three don't fit one game; Cleo and the pair do.
        var match = queue.TryMatch(300, 1, 30);

        Assert.IsNotNull(match);
        CollectionAssert.AreEqual(new[] { Cleo, Bob + 10, Bob + 11 }, match.Players.ToList());

        // The three are a game on their own.
        var next = queue.TryMatch(300, 2, 30);

        Assert.IsNotNull(next);
        CollectionAssert.AreEqual(new[] { Alice, Bob, Dan }, next.Players.ToList());
    }

    [TestMethod]
    public void Queue_StartsWithThoseWhoAccepted()
    {
        var queue = new VehicleGameQueue(Queue(maxPlayers: 3));

        queue.Add(Request(1, 0, Alice));
        queue.Add(Request(2, 0, Bob));
        queue.Add(Request(3, 0, Cleo));

        var match = queue.TryMatch(0, 5, 30)!;

        Assert.IsNotNull(queue.Answer(5, Alice, accept: true));
        Assert.IsNull(queue.Answer(5, Alice, accept: false), "One answer each.");
        Assert.IsNull(queue.Answer(6, Bob, accept: true), "Another match's invitation.");
        Assert.IsNotNull(queue.Answer(5, Bob, accept: false));

        Assert.AreEqual(0, queue.TakeDecided(10_000).Count, "Cleo has until the time is up.");

        var decided = queue.TakeDecided(30_000);

        Assert.AreEqual(1, decided.Count);
        Assert.AreSame(match, decided[0]);
        Assert.IsFalse(queue.Holds(Alice));

        CollectionAssert.AreEqual(new[] { (Alice, 0) }, VehicleGameQueue.Sides(match, 1));
    }

    [TestMethod]
    public void Queue_LeavingAndLoggingOff()
    {
        var queue = new VehicleGameQueue(Queue(maxPlayers: 5));

        var group = Request(1, 0, Alice, Bob, Cleo);
        queue.Add(group);

        // A member leaves alone; the leader takes the rest away.
        Assert.IsTrue(queue.Leave(group, Bob, out var whole));
        Assert.IsFalse(whole);
        CollectionAssert.AreEqual(new[] { Alice, Cleo }, group.Players);

        Assert.IsTrue(queue.Leave(group, Alice, out whole));
        Assert.IsTrue(whole);
        Assert.AreEqual(0, queue.Requests.Count);

        // Logged off while asked in: that's a no.
        queue.Add(Request(2, 0, Dan));
        queue.Add(Request(3, 0, Bob));

        var match = queue.TryMatch(60_000, 9, 30)!;

        queue.Answer(9, Bob, accept: true);
        Assert.IsNull(queue.Forget(Dan, out _), "Dan was asked in, not waiting.");

        var decided = queue.TakeDecided(60_001);

        Assert.AreEqual(1, decided.Count, "Everyone has answered.");
        CollectionAssert.AreEqual(new[] { (Bob, 0) }, VehicleGameQueue.Sides(match, 1));
    }

    [TestMethod]
    public void Queue_SpreadsSoccerRequestsOverTheTeams()
    {
        var queue = new VehicleGameQueue(Queue(maxPlayers: 3, maxTeams: 2));

        queue.Add(Request(1, 0, Alice, Bob));
        queue.Add(Request(2, 0, Cleo));
        queue.Add(Request(3, 0, Dan));

        var match = queue.TryMatch(30_000, 1, 30)!;

        foreach (var guid in match.Players)
            queue.Answer(1, guid, accept: true);

        CollectionAssert.AreEqual(new[] { (Alice, 0), (Bob, 0), (Cleo, 1), (Dan, 1) }, VehicleGameQueue.Sides(match, 2));
    }

    [TestMethod]
    public void Queues_Load()
    {
        var resources = DerbyTests.FindSourceResources();
        var games = DerbyTests.LoadGames();

        var npcIds = JsonDocument.Parse(File.ReadAllText(Path.Combine(resources, "Npcs.json"))).RootElement.EnumerateArray()
            .Select(npc => npc.GetProperty("Id").GetInt32()).ToHashSet();

        // The live queues of the games Sanctuary runs, in the live order.
        CollectionAssert.AreEqual(new[] { 11, 21, 31 }, games.Queues.Select(queue => queue.Id).ToList());

        foreach (var queue in games.Queues)
        {
            foreach (var stageId in queue.StageIds)
            {
                Assert.IsTrue(games.TryGetStage(stageId, out var stage));
                Assert.AreEqual(queue.MatchType, stage.MiniGameType, $"{queue.Comment}: stage {stageId} is another game.");
                Assert.AreEqual(queue.Params[6], (int)stage.Kind, $"{queue.Comment}: Param7 is the encounter kind.");
            }

            foreach (var npcId in queue.LobbyNpcIds)
                Assert.IsTrue(npcIds.Contains(npcId), $"{queue.Comment}: no lobby NPC {npcId}.");

            Assert.IsTrue(queue.MatchSize <= PlayerGroup.MaxMembers * queue.MaxTeams);
        }
    }

    private static VehicleGameQueueDefinition Queue(int maxPlayers, int maxTeams = 1) => new()
    {
        Id = 21,
        Comment = "test",
        MaxPlayers = maxPlayers,
        MaxTeams = maxTeams,
        FillSeconds = 30,
        StageIds = [28]
    };

    private static QueuedRequest Request(ulong id, long queuedAt, params ulong[] players) => new()
    {
        Id = id,
        LeaderGuid = players[0],
        Players = [.. players],
        QueuedAt = queuedAt
    };

    #endregion

    #region The lobby's packets

    /// <summary>
    /// The soccer row of the live queue list, byte for byte (every live ListQueuesResponse was the same 406 bytes).
    /// </summary>
    [TestMethod]
    public void QueueRow_IsTheLiveSoccerRow()
    {
        var soccer = DerbyTests.LoadGames().Queues.Single(queue => queue.Id == 11);

        using var writer = new PacketWriter();

        VehicleGameManager.GetQueueRow(soccer).Serialize(writer);

        var live = Convert.FromHexString(
            "0b000000" + "36790000" + "0c000000" + "01000000" + "03000000" + "01000000" + "02000000" + "78000000" +
            "00000000" + "03000000" + "00000000" + "08000000" + "3b940000" + "00520000" + "04000000" + "3b940000" +
            "00520000" + "08000000" + "00000000" + "00" + "01");

        CollectionAssert.AreEqual(live, writer.Buffer);
    }

    [TestMethod]
    public void QueueList_HasTheLiveHeaderAndRows()
    {
        var games = DerbyTests.LoadGames();

        var packet = new MatchmakingPacketListQueuesResponse
        {
            Guid = Alice,
            Queues = games.Queues.Select(VehicleGameManager.GetQueueRow).ToList()
        }.Serialize();

        CollectionAssert.AreEqual(Convert.FromHexString("8d000200" + "91ca73956acc524b" + "03000000"), packet[..16]);
        Assert.AreEqual(16 + 3 * 78, packet.Length);
    }

    /// <summary>
    /// The client's Quick Play: 73 bytes with no participants and no name. The server needs only the queue.
    /// </summary>
    [TestMethod]
    public void AddMatchRequest_ReadsTheClientsRequest()
    {
        var request = new MatchmakingRequest { QueueId = 21, RequestingPlayerGuid = Alice, GroupId = 0, CreationTime = 1396310000 };

        using var writer = new PacketWriter();

        writer.Write(BaseMatchmakingPacket.OpCode);
        writer.Write((short)3);
        request.Serialize(writer);

        var data = writer.Buffer;

        Assert.AreEqual(73, data.Length);
        Assert.IsTrue(MatchmakingPacketAddMatchRequest.TryDeserialize(data, out var add));
        Assert.AreEqual(21, add.Request.QueueId);
        Assert.AreEqual(Alice, add.Request.RequestingPlayerGuid);

        Assert.IsFalse(MatchmakingPacketAddMatchRequest.TryDeserialize(data[..20], out _));
    }

    [TestMethod]
    public void MatchmakingPackets_HaveTheClientsLayouts()
    {
        var invitation = new MatchmakingPacketMatchInvitationRequest { InvitationId = 7, NameId = 426304, Timeout = 30 }.Serialize();

        CollectionAssert.AreEqual(Convert.FromHexString("8d000900" + "0700000000000000" + "0000000000000000" + "40810600" + "1e000000"), invitation);

        CollectionAssert.AreEqual(Convert.FromHexString("8d000c00" + "15000000"), new MatchmakingPacketSelectQueueForUser { QueueId = 21 }.Serialize());

        var stats = new MatchmakingPacketQueueStatsResponse { Guid = 1, AverageWait = [30, 0], PendingRequests = [2, 0] }.Serialize();

        CollectionAssert.AreEqual(Convert.FromHexString("8d000e00" + "0100000000000000" + "02000000" + "1e000000" + "00000000" + "02000000" + "02000000" + "00000000"), stats);

        CollectionAssert.AreEqual(Convert.FromHexString("8d001200" + "0000000000000000" + "1f000000"),
            new MatchmakingPacketRemoveParticipantFromQueueResponse { QueueId = 31 }.Serialize());

        // The client's answer to a found match.
        var answer = Convert.FromHexString("8d000a00" + "0700000000000000" + "01" + "91ca73956acc524b");

        Assert.IsTrue(MatchmakingPacketMatchInvitationResponse.TryDeserialize(answer, out var response));
        Assert.AreEqual(7UL, response.InvitationId);
        Assert.IsTrue(response.Accept);
        Assert.AreEqual(Alice, response.PlayerGuid);

        Assert.IsTrue(MatchmakingPacketCancelMatchRequest.TryDeserialize(Convert.FromHexString("8d000600" + "0500000000000000"), out var cancel));
        Assert.AreEqual(5UL, cancel.RequestId);
    }

    #endregion

    private static ArenaParticipant Participant(ulong guid) => new(new Player(null!, null!, null!, null!) { Guid = guid });
}
