using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.VehicleGames;

public enum ArenaMatchState
{
    /// <summary>
    /// The start panel is up; nobody has gone in yet.
    /// </summary>
    Offered,

    Running,

    /// <summary>
    /// The results are up; players leave as they close them.
    /// </summary>
    Over,

    Closed
}

public sealed class ArenaParticipant(Player player)
{
    public Player Player { get; } = player;

    public ulong Guid => Player.Guid;

    public bool Entered { get; set; }

    /// <summary>
    /// The game's zone has loaded for them (ClientIsReady): their game is switched on.
    /// </summary>
    public bool InZone { get; set; }

    /// <summary>
    /// Their loading screen is down (ClientFinishedLoading): the HUD is up.
    /// </summary>
    public bool Arrived { get; set; }

    public bool Left { get; set; }

    /// <summary>
    /// The side they'd rather play on, where a game has sides (soccer's teams): a group plays together, queued
    /// players are spread out.
    /// </summary>
    public int Side { get; init; }

    /// <summary>
    /// Where they were when they went in, to go back to.
    /// </summary>
    public int ReturnZoneId { get; set; }
    public ulong? ReturnOwnerId { get; set; }
    public Vector4 ReturnPosition { get; set; }
    public Quaternion ReturnRotation { get; set; }

    /// <summary>
    /// How they did, 0 the best, once the match is over (-1 until then); the end screen and the payout go by it.
    /// </summary>
    public int Place { get; set; } = -1;

    public bool Won { get; set; }

    public bool Rewarded { get; set; }
}

/// <summary>
/// One of the 3D games being played: its own copy of the game's zone, the players in it and the game's own
/// simulation. The <see cref="VehicleGameManager"/> runs the encounter around it (the start panel, going in and out)
/// and calls it under its own lock, 20 times a second.
/// </summary>
public abstract class ArenaMatch
{
    /// <summary>
    /// Every packet of a game's processor is dropped by the client if a byte is missing or left over, and it acts on
    /// game states in order, acknowledging each: a client that never answers (gone, or stuck loading) is waited for
    /// this long before the game carries on without it.
    /// </summary>
    public const int AckTimeoutMs = 30_000;

    /// <summary>
    /// How long a game holds its start, after the first player is in, for the others who are coming (group members
    /// still deciding, on the start panel or loading). After that it starts with those who are there.
    /// </summary>
    public const int JoinWaitMs = 45_000;

    protected ArenaMatch(int id, VehicleGameStageDefinition stage, WorldZone zone, VehicleGameManager manager, ILogger logger)
    {
        Id = id;
        Stage = stage;
        Zone = zone;
        Manager = manager;
        Logger = logger;
    }

    /// <summary>
    /// The encounter id the client is given.
    /// </summary>
    public int Id { get; }

    public VehicleGameStageDefinition Stage { get; }

    public WorldZone Zone { get; }

    protected VehicleGameManager Manager { get; }

    protected ILogger Logger { get; }

    public ArenaMatchState State { get; set; }

    public Dictionary<ulong, ArenaParticipant> Participants { get; } = [];

    /// <summary>
    /// Group members asked along who haven't answered yet, and when their invitation lapses.
    /// </summary>
    public Dictionary<ulong, long> Invited { get; } = [];

    /// <summary>
    /// Everyone the game waits for before it starts: the players who took the offer and haven't left, and those
    /// still deciding.
    /// </summary>
    public IEnumerable<ulong> Expected =>
        Participants.Values.Where(participant => !participant.Left).Select(participant => participant.Guid).Concat(Invited.Keys);

    /// <summary>
    /// The game was set up with the players who were there: nobody else can join it now.
    /// </summary>
    public bool Started { get; protected set; }

    /// <summary>
    /// The players told who the game is waiting for.
    /// </summary>
    public bool ToldWaiting { get; set; }

    /// <summary>
    /// How many players the game has places for (the rest are AI).
    /// </summary>
    public virtual int Capacity => 1;

    public long OfferedAt { get; set; }
    public long StartedAt { get; set; }
    public long OverAt { get; set; }

    /// <summary>
    /// The character state bit that switches the game on in the player's client (0 for the garage).
    /// </summary>
    public abstract int CharacterStateBit { get; }

    /// <summary>
    /// The players in the zone, whose clients run the game.
    /// </summary>
    public IEnumerable<ArenaParticipant> Playing => Participants.Values.Where(participant => participant.InZone && !participant.Left);

    /// <summary>
    /// The player has a place in the game (a car, a kart, a footballer). Before it starts nobody has; once it has,
    /// anyone without one is too late.
    /// </summary>
    public virtual bool HasPlace(ulong guid) => true;

    /// <summary>
    /// The game's zone has loaded for a player: the encounter's packets are sent, then this.
    /// </summary>
    public virtual void OnZoneLoaded(ArenaParticipant participant)
    {
    }

    /// <summary>
    /// A player's loading screen is down.
    /// </summary>
    public virtual void OnArrived(ArenaParticipant participant)
    {
    }

    /// <summary>
    /// One of the game's packets from a player (opcode 33, 34, 76 or 139, read whole).
    /// </summary>
    public abstract void OnPacket(ArenaParticipant participant, short opCode, ReadOnlySpan<byte> data);

    /// <summary>
    /// A tick: <paramref name="deltaSeconds"/> since the last.
    /// </summary>
    public abstract void Update(long now, float deltaSeconds);

    /// <summary>
    /// A player left the match (quit, logged off, teleported away); they're still in <see cref="Participants"/>.
    /// </summary>
    public virtual void OnLeft(ArenaParticipant participant)
    {
    }

    /// <summary>
    /// The match is closing: whatever it spawned goes.
    /// </summary>
    public virtual void OnClosed()
    {
    }

    /// <summary>
    /// Ends the match: each player's <see cref="ArenaParticipant.Place"/> and <see cref="ArenaParticipant.Won"/>
    /// are set by the game; the manager pays them.
    /// </summary>
    protected void Finish()
    {
        if (State is ArenaMatchState.Over or ArenaMatchState.Closed)
            return;

        State = ArenaMatchState.Over;
        OverAt = Environment.TickCount64;

        Manager.Reward(this);
    }

    protected void SendToPlaying(ISerializablePacket packet)
    {
        foreach (var participant in Playing)
            participant.Player.SendTunneled(packet);
    }

    protected void SendToPlaying(ISerializablePacket packet, ulong except, ulong except2 = 0)
    {
        foreach (var participant in Playing)
        {
            if (participant.Guid != except && participant.Guid != except2)
                participant.Player.SendTunneled(packet);
        }
    }

    protected void SendToPlayingUnreliable(ISerializablePacket packet, ulong except = 0)
    {
        foreach (var participant in Playing)
        {
            if (participant.Guid != except)
                participant.Player.SendTunneledUnreliable(packet);
        }
    }
}
