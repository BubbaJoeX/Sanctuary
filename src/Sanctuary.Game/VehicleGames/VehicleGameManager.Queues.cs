using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Interactions;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.VehicleGames;

/// <summary>
/// The lobby's matchmaking queues (BaseMatchmakingPacket, 141), the client's own front end for playing these games
/// with others. The lobby lists the queues (Kart Racing, Demo Derby, Soccer, as live); a game NPC's Lobby option opens
/// it on its game's Quick Play page. Quick Play puts the player (a group's leader, the group) in line, shown in the
/// Quick Match window. When the queue holds a game's worth of players, or the oldest has waited a while, each is asked
/// "Join [game]?"; those who accept get the start panel of one game together, and the AI take the places left.
/// </summary>
public sealed partial class VehicleGameManager
{
    // How long a found match waits for its players to say they're in, and how often the queues are looked at.
    private const int MatchAnswerSeconds = 30;
    private const int QueueIntervalMs = 500;

    private readonly Lock _queueLock = new();

    private IReadOnlyList<VehicleGameQueueDefinition>? _queueDefinitions;
    private List<VehicleGameQueue> _queues = [];

    private ulong _lastRequestId;
    private ulong _lastInvitationId;
    private long _queuesUpdatedAt;

    /// <summary>
    /// The queues, under the queue lock. A reload of VehicleGames.json rebuilds them; those waiting stay in line.
    /// </summary>
    private List<VehicleGameQueue> Queues
    {
        get
        {
            var definitions = _resourceManager.VehicleGames.Queues;

            if (ReferenceEquals(definitions, _queueDefinitions))
                return _queues;

            var old = _queues;

            _queueDefinitions = definitions;
            _queues = definitions.Select(definition => new VehicleGameQueue(definition)).ToList();

            foreach (var queue in old)
            {
                if (_queues.FirstOrDefault(kept => kept.Definition.Id == queue.Definition.Id) is not { } kept)
                    continue;

                foreach (var request in queue.Requests.Concat(queue.Matches.SelectMany(match => match.Requests)))
                {
                    if (request.Players.Count <= kept.Definition.MatchSize)
                        kept.Add(request);
                }
            }

            return _queues;
        }
    }

    /// <summary>
    /// The lobby's row for a queue, as live listed it.
    /// </summary>
    public static MatchmakingQueueDefinition GetQueueRow(VehicleGameQueueDefinition queue) => new()
    {
        QueueId = queue.Id,
        NameId = queue.NameId,
        MatchType = queue.MatchType,
        MinPlayers = queue.MinPlayers,
        MaxPlayers = queue.MaxPlayers,
        MinTeams = queue.MinTeams,
        MaxTeams = queue.MaxTeams,
        MaxGameStartDelay = queue.MaxGameStartDelay,
        Param1 = queue.Params[0],
        Param2 = queue.Params[1],
        Param3 = queue.Params[2],
        Param4 = queue.Params[3],
        Param5 = queue.Params[4],
        Param6 = queue.Params[5],
        Param7 = queue.Params[6],
        EncounterDescriptionId = queue.DescriptionId,
        EncounterIconId = queue.IconId,
        Unknown48 = queue.ProfileType,
        Unknown51 = true
    };

    /// <summary>
    /// A game NPC's Lobby option (live: "Kart Race Lobby", "Demo Derby Lobby", soccer's "Game Lobby").
    /// </summary>
    private IEnumerable<NpcInteractionOption> GetLobbyOptions(Npc npc) =>
        _resourceManager.VehicleGames.Queues.Where(queue => queue.LobbyNpcIds.Contains(npc.DefinitionId)).Select(queue => new NpcInteractionOption
        {
            IconId = queue.LobbyIconId,
            ButtonTextId = queue.LobbyTextId,
            Invoke = player => OpenLobby(player, queue.Id)
        });

    /// <summary>
    /// The lobby on the queue's Quick Play page, with the queue list first so the page has its queue.
    /// </summary>
    private void OpenLobby(Player player, int queueId)
    {
        lock (_queueLock)
            SendQueues(player, player.Guid);

        player.SendTunneled(new MatchmakingPacketSelectQueueForUser { QueueId = queueId });
    }

    public void HandleMatchmakingPacket(Player player, ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
            return;

        lock (_queueLock)
        {
            switch (BinaryPrimitives.ReadInt16LittleEndian(data[2..]))
            {
                case MatchmakingPacketListQueuesRequest.OpCode when MatchmakingPacketListQueuesRequest.TryDeserialize(data, out var list):
                    SendQueues(player, list.PlayerGuid);
                    break;

                case MatchmakingPacketQueueStatsRequest.OpCode when MatchmakingPacketQueueStatsRequest.TryDeserialize(data, out _):
                    SendQueueStats(player);
                    break;

                case MatchmakingPacketAddMatchRequest.OpCode when MatchmakingPacketAddMatchRequest.TryDeserialize(data, out var add):
                    Enqueue(player, add.Request.QueueId);
                    break;

                case MatchmakingPacketCancelMatchRequest.OpCode when MatchmakingPacketCancelMatchRequest.TryDeserialize(data, out var cancel):
                    Cancel(player, cancel.RequestId);
                    break;

                case MatchmakingPacketMatchInvitationResponse.OpCode when MatchmakingPacketMatchInvitationResponse.TryDeserialize(data, out var answer):
                    foreach (var queue in Queues)
                    {
                        if (queue.Answer(answer.InvitationId, player.Guid, answer.Accept) is not null)
                            break;
                    }
                    break;

                default:
                    _logger.LogInformation("{player} sent a matchmaking packet. ( Data: {data} )", player.Name.FullName, Convert.ToHexString(data));
                    break;
            }
        }
    }

    #region Requests

    private void SendQueues(Player player, ulong guid) => player.SendTunneled(new MatchmakingPacketListQueuesResponse
    {
        Guid = guid,
        Queues = Queues.Select(queue => GetQueueRow(queue.Definition)).ToList()
    });

    private void SendQueueStats(Player player) => player.SendTunneled(new MatchmakingPacketQueueStatsResponse
    {
        Guid = player.Guid,
        AverageWait = Queues.Select(queue => queue.AverageWaitSeconds).ToList(),
        PendingRequests = Queues.Select(queue => queue.Waiting).ToList()
    });

    /// <summary>
    /// Quick Play: the player in line, or their whole group if they lead one (as many as the queue takes a team).
    /// </summary>
    private void Enqueue(Player player, int queueId)
    {
        if (Queues.FirstOrDefault(queue => queue.Definition.Id == queueId) is not { } queue)
            return;

        if (Queues.Any(other => other.Holds(player.Guid)))
        {
            ChatHelper.SendSystemMessage(player, "You're already in line for a game.");
            return;
        }

        if (!CanPlay(player, tell: true))
            return;

        var players = new List<Player> { player };
        var group = player.Group;

        if (group is not null && group.LeaderGuid == player.Guid)
        {
            foreach (var memberGuid in group.Members)
            {
                if (players.Count >= queue.Definition.MaxPlayers)
                    break;

                if (memberGuid != player.Guid && _zoneManager.TryGetPlayer(memberGuid, out var member) && CanPlay(member, tell: false)
                    && !Queues.Any(other => other.Holds(memberGuid)))
                    players.Add(member);
            }
        }

        var request = new QueuedRequest
        {
            Id = ++_lastRequestId,
            LeaderGuid = player.Guid,
            Players = players.Select(queued => queued.Guid).ToList(),
            GroupId = players.Count > 1 ? group!.Id : -1,
            CreationTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            QueuedAt = Environment.TickCount64
        };

        queue.Add(request);

        // Everyone in it sees it in their Quick Match window ("[leader] has put you in line" for the others).
        var response = new MatchmakingPacketAddMatchRequestResponse { Request = GetRequest(queue, request, player) };

        foreach (var queued in players)
            queued.SendTunneled(response);

        _logger.LogInformation("{players} queued for {queue}.", string.Join(", ", players.Select(queued => queued.Name.FullName)), queue.Definition.Comment);
    }

    /// <summary>
    /// The Quick Match window's leave: the player's out of line (a leader takes their group with them). Already
    /// asked in, it's a no.
    /// </summary>
    private void Cancel(Player player, ulong requestId)
    {
        foreach (var queue in Queues)
        {
            if (queue.FindRequest(requestId) is { } request)
            {
                if (queue.Leave(request, player.Guid, out var whole))
                    Clear(queue, request, whole ? request.Players : [player.Guid]);

                return;
            }

            if (queue.Matches.FirstOrDefault(match => match.Requests.Any(matched => matched.Id == requestId)) is { } asked)
            {
                queue.Answer(asked.InvitationId, player.Guid, accept: false);
                return;
            }
        }
    }

    /// <summary>
    /// The player is off to a game (or gone): out of any queue.
    /// </summary>
    private void LeaveQueues(Player player)
    {
        lock (_queueLock)
        {
            foreach (var queue in Queues)
                Forget(queue, player.Guid);
        }
    }

    private void Forget(VehicleGameQueue queue, ulong guid)
    {
        if (queue.Forget(guid, out var whole) is { } request)
            Clear(queue, request, whole ? request.Players : [guid]);
    }

    /// <summary>
    /// Takes the request off the players' Quick Match windows, quietly.
    /// </summary>
    private void Clear(VehicleGameQueue queue, QueuedRequest request, IEnumerable<ulong> players)
    {
        var clear = new MatchmakingPacketClearMatchRequest { Request = GetRequest(queue, request, null) };

        foreach (var guid in players)
        {
            if (_zoneManager.TryGetPlayer(guid, out var player))
                player.SendTunneled(clear);
        }
    }

    private MatchmakingRequest GetRequest(VehicleGameQueue queue, QueuedRequest request, Player? leader)
    {
        if (leader is null)
            _zoneManager.TryGetPlayer(request.LeaderGuid, out leader);

        return new MatchmakingRequest
        {
            RequestId = request.Id,
            QueueId = queue.Definition.Id,
            RequestingPlayerGuid = request.LeaderGuid,
            GroupId = request.GroupId,
            QueueParam1 = queue.Definition.Params[0],
            CreationTime = request.CreationTime,
            PlayerName = leader?.Name ?? new NameData()
        };
    }

    #endregion

    #region Matches

    /// <summary>
    /// Twice a second: those who logged off leave the queues, full (or long waiting) queues make matches and ask
    /// their players in, and matches everyone answered start, outside the queue lock.
    /// </summary>
    private void UpdateQueues(long now)
    {
        if (now - _queuesUpdatedAt < QueueIntervalMs)
            return;

        _queuesUpdatedAt = now;

        var decided = new List<(VehicleGameQueue Queue, QueuedMatch Match)>();

        lock (_queueLock)
        {
            foreach (var queue in Queues)
            {
                var queued = queue.Requests.SelectMany(request => request.Players).Concat(queue.Matches.SelectMany(match => match.Players)).Distinct().ToList();

                foreach (var guid in queued)
                {
                    if (!_zoneManager.TryGetPlayer(guid, out _))
                        Forget(queue, guid);
                }

                while (queue.TryMatch(now, ++_lastInvitationId, MatchAnswerSeconds) is { } match)
                    Ask(queue, match);

                foreach (var match in queue.TakeDecided(now))
                {
                    Dismiss(queue, match);
                    decided.Add((queue, match));
                }
            }
        }

        foreach (var (queue, match) in decided)
            Start(queue, match);
    }

    /// <summary>
    /// "Join [game]?" for every player matched.
    /// </summary>
    private void Ask(VehicleGameQueue queue, QueuedMatch match)
    {
        var invitation = new MatchmakingPacketMatchInvitationRequest
        {
            InvitationId = match.InvitationId,
            NameId = queue.Definition.NameId,
            Timeout = MatchAnswerSeconds
        };

        foreach (var guid in match.Players)
        {
            if (_zoneManager.TryGetPlayer(guid, out var player))
                player.SendTunneled(invitation);
        }

        _logger.LogInformation("{queue} matched {count} player(s) as {invitation}.", queue.Definition.Comment, match.Players.Count(), match.InvitationId);
    }

    /// <summary>
    /// The match is decided: the group window's timer closes and the requests leave the Quick Match window, quietly
    /// for those who answered, with "You have been removed from the matchmaking queue." for those who didn't.
    /// </summary>
    private void Dismiss(VehicleGameQueue queue, QueuedMatch match)
    {
        var closed = new MatchmakingPacketMatchInvitationRequest { InvitationId = match.InvitationId };
        var removed = new MatchmakingPacketRemoveParticipantFromQueueResponse { QueueId = queue.Definition.Id };

        foreach (var request in match.Requests)
        {
            var clear = new MatchmakingPacketClearMatchRequest { Request = GetRequest(queue, request, null) };

            foreach (var guid in request.Players)
            {
                if (!_zoneManager.TryGetPlayer(guid, out var player))
                    continue;

                player.SendTunneled(closed);
                player.SendTunneled(match.Answers.ContainsKey(guid) ? clear : removed);
            }
        }
    }

    /// <summary>
    /// The players who accepted get one of the queue's games together, each request on a side of its own where the
    /// game has sides.
    /// </summary>
    private void Start(VehicleGameQueue queue, QueuedMatch match)
    {
        var players = new List<(Player Player, int Side)>();

        foreach (var (guid, side) in VehicleGameQueue.Sides(match, queue.Definition.MaxTeams))
        {
            if (_zoneManager.TryGetPlayer(guid, out var player))
                players.Add((player, side));
        }

        if (players.Count == 0)
            return;

        var stageIds = queue.Definition.StageIds;

        if (_resourceManager.VehicleGames.TryGetStage(stageIds[Random.Shared.Next(stageIds.Count)], out var stage) && Launch(stage, players))
            return;

        foreach (var (player, _) in players)
            ChatHelper.SendSystemMessage(player, "That game failed to start. Try again in a moment.");
    }

    #endregion
}
