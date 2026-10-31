using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Core.IO;
using Sanctuary.Packet;

namespace Sanctuary.Game.Tests;

/// <summary>
/// The 3D games' packets against the live captures (VehicleGameCaptures.json: a sample or two of every sub-op seen
/// in the 2014 demolition derby, kart race and soccer match): each must read whole and write back byte for byte, as
/// the client reads every one of them strictly.
/// </summary>
[TestClass]
public class VehicleGamePacketTests
{
    private delegate byte[]? RoundTrip(byte[] data);

    private static RoundTrip Of<T>(TryDeserializer<T> tryDeserialize) where T : ISerializablePacket =>
        data => tryDeserialize(data, out var packet) ? packet.Serialize() : null;

    private delegate bool TryDeserializer<T>(ReadOnlySpan<byte> data, out T value);

    private static readonly Dictionary<string, RoundTrip> Derby = new()
    {
        ["S>C 1"] = Of<VehicleDemolitionDerbyPacketUpdateVehicleGameState>(VehicleDemolitionDerbyPacketUpdateVehicleGameState.TryDeserialize),
        ["C>S 2"] = Of<VehicleDemolitionDerbyPacketUpdateVehicleGamePlayerState>(VehicleDemolitionDerbyPacketUpdateVehicleGamePlayerState.TryDeserialize),
        ["S>C 3"] = Of<VehicleDemolitionDerbyPacketUpdatePlayerCount>(VehicleDemolitionDerbyPacketUpdatePlayerCount.TryDeserialize),
        ["S>C 4"] = Of<VehicleDemolitionDerbyPacketUpdateNpcCount>(VehicleDemolitionDerbyPacketUpdateNpcCount.TryDeserialize),
        ["S>C 5"] = Of<VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyData>(VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyData.TryDeserialize),
        ["C>S 5"] = Of<VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyData>(VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyData.TryDeserialize),
        ["C>S 6"] = Of<VehicleDemolitionDerbyPacketRegisterVehicleDemolitionDerbyPlayer>(VehicleDemolitionDerbyPacketRegisterVehicleDemolitionDerbyPlayer.TryDeserialize),
        ["S>C 7"] = Of<VehicleDemolitionDerbyPacketVehicleDemolitionDerbyCountdownStatus>(VehicleDemolitionDerbyPacketVehicleDemolitionDerbyCountdownStatus.TryDeserialize),
        ["C>S 7"] = Of<VehicleDemolitionDerbyPacketVehicleDemolitionDerbyCountdownStatus>(VehicleDemolitionDerbyPacketVehicleDemolitionDerbyCountdownStatus.TryDeserialize),
        ["S>C 10"] = Of<VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerby>(VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerby.TryDeserialize),
        ["S>C 11"] = Of<VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyStateData>(VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyStateData.TryDeserialize),
        ["S>C 13"] = Of<VehicleDemolitionDerbyPacketDestroyProxiedVehicleDemolitionDerbyPickup>(VehicleDemolitionDerbyPacketDestroyProxiedVehicleDemolitionDerbyPickup.TryDeserialize),
        ["C>S 14"] = Of<VehicleDemolitionDerbyPacketPickupHitRequest>(VehicleDemolitionDerbyPacketPickupHitRequest.TryDeserialize),
        ["S>C 15"] = Of<VehicleDemolitionDerbyPacketPickupHitResponse>(VehicleDemolitionDerbyPacketPickupHitResponse.TryDeserialize),
        ["C>S 16"] = Of<VehicleDemolitionDerbyPacketVehicleHit>(VehicleDemolitionDerbyPacketVehicleHit.TryDeserialize),
        ["S>C 18"] = Of<VehicleDemolitionDerbyPacketResetProxiedVehicleDemolitionDerby>(VehicleDemolitionDerbyPacketResetProxiedVehicleDemolitionDerby.TryDeserialize),
        ["S>C 19"] = Of<VehicleDemolitionDerbyPacketSetDerbyConfig>(VehicleDemolitionDerbyPacketSetDerbyConfig.TryDeserialize),
        ["S>C 20"] = Of<VehicleDemolitionDerbyPacketAnnouncerMessage>(VehicleDemolitionDerbyPacketAnnouncerMessage.TryDeserialize),
    };

    private static readonly Dictionary<string, RoundTrip> Race = new()
    {
        ["S>C 1"] = Of<VehicleRacePacketUpdateVehicleGameState>(VehicleRacePacketUpdateVehicleGameState.TryDeserialize),
        ["C>S 2"] = Of<VehicleRacePacketUpdateVehicleGamePlayerState>(VehicleRacePacketUpdateVehicleGamePlayerState.TryDeserialize),
        ["S>C 3"] = Of<VehicleRacePacketUpdatePlayerCount>(VehicleRacePacketUpdatePlayerCount.TryDeserialize),
        ["S>C 4"] = Of<VehicleRacePacketUpdateNpcCount>(VehicleRacePacketUpdateNpcCount.TryDeserialize),
        ["S>C 5"] = Of<VehicleRacePacketUpdateVehicleRaceClientData>(VehicleRacePacketUpdateVehicleRaceClientData.TryDeserialize),
        ["C>S 6"] = Of<VehicleRacePacketUpdateVehicleRaceServerData>(VehicleRacePacketUpdateVehicleRaceServerData.TryDeserialize),
        ["C>S 7"] = Of<VehicleRacePacketGateHit>(VehicleRacePacketGateHit.TryDeserialize),
        ["S>C 8"] = Of<VehicleRacePacketVehicleHit>(VehicleRacePacketVehicleHit.TryDeserialize),
        ["C>S 8"] = Of<VehicleRacePacketVehicleHit>(VehicleRacePacketVehicleHit.TryDeserialize),
        ["C>S 9"] = Of<VehicleRacePacketRegisterVehicleRacePlayer>(VehicleRacePacketRegisterVehicleRacePlayer.TryDeserialize),
        ["S>C 10"] = Of<VehicleRacePacketVehicleRaceCountdownStatus>(VehicleRacePacketVehicleRaceCountdownStatus.TryDeserialize),
        ["C>S 10"] = Of<VehicleRacePacketVehicleRaceCountdownStatus>(VehicleRacePacketVehicleRaceCountdownStatus.TryDeserialize),
        ["S>C 11"] = Of<VehicleRacePacketCreateProxiedVehicleRace>(VehicleRacePacketCreateProxiedVehicleRace.TryDeserialize),
        ["S>C 14"] = Of<VehicleRacePacketUpdateVehicleRaceProgressData>(VehicleRacePacketUpdateVehicleRaceProgressData.TryDeserialize),
        ["S>C 15"] = Of<VehicleRacePacketCreateProxiedVehicleRacePickup>(VehicleRacePacketCreateProxiedVehicleRacePickup.TryDeserialize),
        ["S>C 16"] = Of<VehicleRacePacketDestroyProxiedVehicleRacePickup>(VehicleRacePacketDestroyProxiedVehicleRacePickup.TryDeserialize),
        ["C>S 17"] = Of<VehicleRacePacketPickupHitRequest>(VehicleRacePacketPickupHitRequest.TryDeserialize),
        ["S>C 18"] = Of<VehicleRacePacketPickupHitResponse>(VehicleRacePacketPickupHitResponse.TryDeserialize),
        ["C>S 22"] = Of<VehicleRacePacketBoostAwardRequest>(VehicleRacePacketBoostAwardRequest.TryDeserialize),
        ["S>C 23"] = Of<VehicleRacePacketBoostAwardResponse>(VehicleRacePacketBoostAwardResponse.TryDeserialize),
        ["S>C 34"] = Of<VehicleRacePacketSetRaceConfig>(VehicleRacePacketSetRaceConfig.TryDeserialize),
    };

    private static readonly Dictionary<string, RoundTrip> Soccer = new()
    {
        ["S>C 1"] = Of<SoccerPacketUpdatePlayerPos>(SoccerPacketUpdatePlayerPos.TryDeserialize),
        ["C>S 1"] = Of<SoccerPacketUpdatePlayerPos>(SoccerPacketUpdatePlayerPos.TryDeserialize),
        ["S>C 2"] = Of<SoccerPacketGoalMade>(SoccerPacketGoalMade.TryDeserialize),
        ["S>C 3"] = Of<SoccerPacketRegisterPlayer>(SoccerPacketRegisterPlayer.TryDeserialize),
        ["C>S 3"] = Of<SoccerPacketRegisterPlayer>(SoccerPacketRegisterPlayer.TryDeserialize),
        ["S>C 4"] = Of<SoccerPacketUpdateSoccerBall>(SoccerPacketUpdateSoccerBall.TryDeserialize),
        ["S>C 5"] = Of<SoccerPacketUpdateGameState>(SoccerPacketUpdateGameState.TryDeserialize),
        ["S>C 6"] = Of<SoccerPacketSetPlayerTeam>(SoccerPacketSetPlayerTeam.TryDeserialize),
        ["S>C 7"] = Of<SoccerPacketSetClientConfig>(SoccerPacketSetClientConfig.TryDeserialize),
        ["S>C 8"] = Of<SoccerPacketTeamColours>(SoccerPacketTeamColours.TryDeserialize),
        ["S>C 9"] = Of<SoccerPacketSpawnPickUp>(SoccerPacketSpawnPickUp.TryDeserialize),
        ["S>C 10"] = Of<SoccerPacketAcquirePickUp>(SoccerPacketAcquirePickUp.TryDeserialize),
        ["S>C 11"] = Of<SoccerPacketAcquireBall>(SoccerPacketAcquireBall.TryDeserialize),
        ["S>C 12"] = Of<SoccerPacketFreeBall>(SoccerPacketFreeBall.TryDeserialize),
        ["S>C 14"] = Of<SoccerPacketUpdatePlayerState>(SoccerPacketUpdatePlayerState.TryDeserialize),
        ["C>S 14"] = Of<SoccerPacketUpdatePlayerState>(SoccerPacketUpdatePlayerState.TryDeserialize),
        ["S>C 15"] = Of<SoccerPacketUpdatePlayerStats>(SoccerPacketUpdatePlayerStats.TryDeserialize),
        ["S>C 16"] = Of<SoccerPacketUpdatePlayerControls>(SoccerPacketUpdatePlayerControls.TryDeserialize),
        ["C>S 16"] = Of<SoccerPacketUpdatePlayerControls>(SoccerPacketUpdatePlayerControls.TryDeserialize),
        ["S>C 18"] = Of<SoccerPacketHighArcBall>(SoccerPacketHighArcBall.TryDeserialize),
        ["S>C 19"] = Of<SoccerPacketPlayerVariableStats>(SoccerPacketPlayerVariableStats.TryDeserialize),
        ["S>C 21"] = Of<SoccerPacketSetSoccerPlayerOneTimeStats>(SoccerPacketSetSoccerPlayerOneTimeStats.TryDeserialize),
        ["S>C 23"] = Of<SoccerPacketUpdateGameTime>(SoccerPacketUpdateGameTime.TryDeserialize),
        ["S>C 25"] = Of<SoccerPacketBallBounce>(SoccerPacketBallBounce.TryDeserialize),
    };

    [TestMethod]
    public void DerbyPackets_RoundTripTheLiveMatch() => AssertRoundTrips("derby", Derby);

    [TestMethod]
    public void RacePackets_RoundTripTheLiveRace() => AssertRoundTrips("race", Race);

    [TestMethod]
    public void SoccerPackets_RoundTripTheLiveMatch() => AssertRoundTrips("soccer", Soccer);

    [TestMethod]
    public void MinigameRules_MatchTheLiveGames()
    {
        foreach (var game in new[] { "derby", "race", "soccer" })
            CollectionAssert.AreEqual(Live(game, "S>C 62")[0], EncounterDataCommonPacket.Minigame.Serialize(), $"{game}'s EncounterDataCommon");

        CollectionAssert.AreEqual(Live("derby", "S>C 39/23")[0], new MiniGameKnockOutPacket().Serialize());
        CollectionAssert.AreEqual(Live("derby", "S>C 39/23")[1], new MiniGameKnockOutPacket { Count = 1 }.Serialize());
    }

    [TestMethod]
    public void VehicleConfig_IsReadWholeAndRepaintsOnlyTheBody()
    {
        var live = Live("race", "S>C 11")[0];
        var reader = new PacketReader(live.AsSpan(4));

        Assert.IsTrue(VehicleConfig.TryRead(ref reader, out var config));

        // The tail after it: guid, rotation rows, position, IsLocalPlayer, grid slot.
        Assert.AreEqual(58, reader.RemainingLength);
        Assert.AreEqual("kt_kart08_body_bbe.cdt", config.CollisionModel);
        Assert.AreEqual("bluestripe", config.BodyTextureAlias);
        Assert.AreEqual("frlogo", config.DecalTextureAlias);
        Assert.AreEqual(100u, config.BodyTintId);

        var repainted = config.Repaint(tintId: 95, bodyTextureAlias: "flower");
        var repaintedReader = new PacketReader(repainted.Data);

        Assert.IsTrue(VehicleConfig.TryRead(ref repaintedReader, out var reread));
        Assert.AreEqual(0, repaintedReader.RemainingLength);
        Assert.AreEqual("flower", reread.BodyTextureAlias);
        Assert.AreEqual(95u, reread.BodyTintId);
        Assert.AreEqual("frlogo", reread.DecalTextureAlias);
        Assert.AreEqual(config.Length + "flower".Length - "bluestripe".Length, repainted.Length);

        // Repainting nothing changes nothing.
        CollectionAssert.AreEqual(config.Data, config.Repaint().Data);
    }

    private static void AssertRoundTrips(string game, Dictionary<string, RoundTrip> layouts)
    {
        var captures = Captures()[game];

        foreach (var (key, roundTrip) in layouts)
        {
            Assert.IsTrue(captures.TryGetValue(key, out var samples), $"{game} {key}: no live sample.");

            foreach (var hex in samples)
            {
                var live = Convert.FromHexString(hex);
                var written = roundTrip(live);

                Assert.IsNotNull(written, $"{game} {key}: the live packet ({live.Length} bytes) doesn't read whole.");
                CollectionAssert.AreEqual(live, written, $"{game} {key}: written back differently.");
            }
        }
    }

    internal static List<byte[]> Live(string game, string key) =>
        Captures()[game][key].Select(Convert.FromHexString).ToList();

    private static Dictionary<string, Dictionary<string, List<string>>>? _captures;

    private static Dictionary<string, Dictionary<string, List<string>>> Captures()
    {
        if (_captures is not null)
            return _captures;

        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Sanctuary.Game.Tests", "VehicleGameCaptures.json")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "src/Sanctuary.Game.Tests/VehicleGameCaptures.json not found above the test output.");

        Document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, "Sanctuary.Game.Tests", "VehicleGameCaptures.json")));

        // The three games' packets by "direction sub"; the file holds replays besides.
        return _captures = new[] { "derby", "race", "soccer" }.ToDictionary(game => game,
            game => Document.RootElement.GetProperty(game).Deserialize<Dictionary<string, List<string>>>()!);
    }

    /// <summary>
    /// The whole captures file, for the replays in it.
    /// </summary>
    internal static JsonDocument Document
    {
        get
        {
            if (_document is null)
                Captures();

            return _document!;
        }
        private set => _document = value;
    }

    private static JsonDocument? _document;
}
