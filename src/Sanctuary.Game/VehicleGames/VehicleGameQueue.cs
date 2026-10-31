using System;
using System.Collections.Generic;
using System.Linq;

using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.VehicleGames;

/// <summary>
/// A place in a queue: one player, or a group its leader queued.
/// </summary>
public sealed class QueuedRequest
{
    public required ulong Id { get; init; }

    public required ulong LeaderGuid { get; init; }

    public required List<ulong> Players { get; init; }

    /// <summary>
    /// The group's id, or -1 for one player.
    /// </summary>
    public int GroupId { get; init; } = -1;

    /// <summary>
    /// Unix seconds, for the Quick Match window's time in the queue.
    /// </summary>
    public long CreationTime { get; init; }

    /// <summary>
    /// When it joined the queue (Environment.TickCount64).
    /// </summary>
    public required long QueuedAt { get; init; }
}

/// <summary>
/// Requests a queue matched up, their players asked whether they're in: each answers, or the time runs out.
/// </summary>
public sealed class QueuedMatch
{
    public required ulong InvitationId { get; init; }

    public required IReadOnlyList<QueuedRequest> Requests { get; init; }

    public required long ExpiresAt { get; init; }

    public Dictionary<ulong, bool> Answers { get; } = [];

    public IEnumerable<ulong> Players => Requests.SelectMany(request => request.Players);

    public bool Answered => Players.All(Answers.ContainsKey);

    public bool Accepted(ulong guid) => Answers.TryGetValue(guid, out var accepted) && accepted;
}

/// <summary>
/// One of the lobby's queues, with no packets: the requests waiting in it, oldest first, and the matches made from
/// them. A match takes as many requests as fit in a game, oldest first, as soon as they fill it, or once the oldest
/// has waited <see cref="VehicleGameQueueDefinition.FillSeconds"/> (the AI fill the places left).
/// </summary>
public sealed class VehicleGameQueue(VehicleGameQueueDefinition definition)
{
    private const int WaitsKept = 10;

    private readonly List<QueuedRequest> _requests = [];
    private readonly List<QueuedMatch> _matches = [];
    private readonly Queue<long> _waits = new();

    public VehicleGameQueueDefinition Definition { get; } = definition;

    public IReadOnlyList<QueuedRequest> Requests => _requests;

    public IReadOnlyList<QueuedMatch> Matches => _matches;

    /// <summary>
    /// The players waiting ("@1 Waiting").
    /// </summary>
    public int Waiting => _requests.Sum(request => request.Players.Count);

    /// <summary>
    /// How long the last few matches' players waited, on average, in seconds.
    /// </summary>
    public int AverageWaitSeconds => _waits.Count == 0 ? 0 : (int)(_waits.Average() / 1000);

    /// <summary>
    /// The player is waiting in the queue, or in one of its matches.
    /// </summary>
    public bool Holds(ulong guid) =>
        _requests.Any(request => request.Players.Contains(guid)) || _matches.Any(match => match.Players.Contains(guid));

    public QueuedRequest? FindRequest(ulong requestId) => _requests.FirstOrDefault(request => request.Id == requestId);

    public void Add(QueuedRequest request) => _requests.Add(request);

    /// <summary>
    /// A player leaves the request: its leader takes it all away, anyone else just themselves. False if it's not
    /// waiting in this queue.
    /// </summary>
    public bool Leave(QueuedRequest request, ulong guid, out bool whole)
    {
        whole = false;

        if (!_requests.Contains(request) || !request.Players.Contains(guid))
            return false;

        if (guid == request.LeaderGuid || request.Players.Count == 1)
        {
            whole = true;
            _requests.Remove(request);
        }
        else
            request.Players.Remove(guid);

        return true;
    }

    /// <summary>
    /// The player is out of the queue (logged off, or into another game): out of their request, or their match's
    /// answer is no. The request they were in, if one was waiting, and whether it went with them (they led it).
    /// </summary>
    public QueuedRequest? Forget(ulong guid, out bool whole)
    {
        whole = false;

        foreach (var match in _matches)
        {
            if (match.Players.Contains(guid))
                match.Answers[guid] = false;
        }

        var request = _requests.FirstOrDefault(request => request.Players.Contains(guid));

        if (request is not null)
            Leave(request, guid, out whole);

        return request;
    }

    /// <summary>
    /// A match from the waiting requests, if it's time for one: oldest first, as many as fit a game.
    /// </summary>
    public QueuedMatch? TryMatch(long now, ulong invitationId, int answerSeconds)
    {
        if (_requests.Count == 0)
            return null;

        var size = Definition.MatchSize;
        var taken = new List<QueuedRequest>();
        var count = 0;

        foreach (var request in _requests)
        {
            if (count + request.Players.Count > size)
                continue;

            taken.Add(request);
            count += request.Players.Count;

            if (count == size)
                break;
        }

        var full = count >= size;
        var waited = taken.Count > 0 && now - taken[0].QueuedAt >= Definition.FillSeconds * 1000L;

        if (!full && !(waited && count >= Definition.MinPlayers))
            return null;

        foreach (var request in taken)
        {
            _requests.Remove(request);

            _waits.Enqueue(now - request.QueuedAt);

            if (_waits.Count > WaitsKept)
                _waits.Dequeue();
        }

        var match = new QueuedMatch
        {
            InvitationId = invitationId,
            Requests = taken,
            ExpiresAt = now + answerSeconds * 1000L
        };

        _matches.Add(match);

        return match;
    }

    /// <summary>
    /// A player answers the match they were asked in to; the match, or null if they weren't asked (or already
    /// answered).
    /// </summary>
    public QueuedMatch? Answer(ulong invitationId, ulong guid, bool accept)
    {
        var match = _matches.FirstOrDefault(match => match.InvitationId == invitationId && match.Players.Contains(guid));

        if (match is null || !match.Answers.TryAdd(guid, accept))
            return null;

        return match;
    }

    /// <summary>
    /// The matches everyone has answered, or whose time is up: they're the queue's no more.
    /// </summary>
    public List<QueuedMatch> TakeDecided(long now)
    {
        var decided = _matches.Where(match => match.Answered || now >= match.ExpiresAt).ToList();

        foreach (var match in decided)
            _matches.Remove(match);

        return decided;
    }

    /// <summary>
    /// Who plays from a decided match, and on which side: those who accepted, each request kept together on the side
    /// with fewer players so far (soccer's teams; the other games have one side).
    /// </summary>
    public static List<(ulong Guid, int Side)> Sides(QueuedMatch match, int teams)
    {
        var counts = new int[Math.Max(1, teams)];
        var sides = new List<(ulong, int)>();

        foreach (var request in match.Requests)
        {
            var accepted = request.Players.Where(match.Accepted).ToList();

            if (accepted.Count == 0)
                continue;

            var side = Array.IndexOf(counts, counts.Min());

            counts[side] += accepted.Count;

            sides.AddRange(accepted.Select(guid => (guid, side)));
        }

        return sides;
    }
}
