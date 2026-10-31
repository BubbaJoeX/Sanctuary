using System;
using System.Collections.Generic;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Interactions;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;

namespace Sanctuary.Game.VehicleGames;

public interface IVehicleGameManager
{
    IReadOnlyList<NpcInteractionOption> GetInteractionOptions(Player player, Npc npc);

    /// <summary>
    /// The Games window's tiles (Karts, Soccer).
    /// </summary>
    IEnumerable<ActivityDefinition> GetActivities();

    /// <summary>
    /// Opens the game's start panel for the player.
    /// </summary>
    bool Offer(Player player, VehicleGameStageDefinition stage);

    /// <summary>
    /// The player is offered or playing one of these games.
    /// </summary>
    bool IsPlaying(Player player);

    /// <summary>
    /// The encounter requests: each is false when the encounter isn't one of these games, for the battles to take.
    /// </summary>
    bool RequestEntrance(Player player, int encounterId);
    bool CancelPending(Player player);
    bool RequestExit(Player player);

    /// <summary>
    /// A zone has loaded for a player (ClientIsReady): a game's zone switches the game on.
    /// </summary>
    void OnClientIsReady(Player player, WorldZone zone);

    /// <summary>
    /// A player's loading screen is down (ClientFinishedLoading).
    /// </summary>
    void OnArrived(Player player, WorldZone zone);

    void OnPlayerLeft(Player player, WorldZone zone);

    /// <summary>
    /// A packet of the games' families (33 kart racing, 34 demolition derby, 76 soccer, 139 vehicle parts), whole.
    /// </summary>
    void HandlePacket(Player player, short opCode, ReadOnlySpan<byte> data);
}
