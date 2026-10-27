using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;

namespace Sanctuary.Game;

/// <summary>
/// Events that staff opened for players to join with !join (see HostChatCommand): where joining sends them,
/// and who joined. Kept in memory only, since an event lasts an evening; ending one forgets its sign-ups.
/// </summary>
public sealed class EventJoinBook
{
    public sealed record JoinPoint(string Key, int ZoneDefinitionId, Vector4 Position, Quaternion Rotation, string HostName);

    public enum FindResult
    {
        Found,
        NoneOpen,
        NotOpen,

        /// <summary>
        /// No key was given and more than one event is open.
        /// </summary>
        Ambiguous
    }

    private readonly ConcurrentDictionary<string, JoinPoint> _open = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<ulong, string>> _joined = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<JoinPoint> Open => [.. _open.Values.OrderBy(point => point.Key)];

    /// <summary>
    /// Opens the event for joining at <paramref name="point"/>, or moves the spot of one already open.
    /// Anyone who joined before keeps their place.
    /// </summary>
    public void OpenJoin(JoinPoint point)
    {
        _open[point.Key] = point;
        _joined.GetOrAdd(point.Key, static _ => new());
    }

    /// <summary>
    /// Stops new joins; the event keeps its sign-ups for prizes.
    /// </summary>
    public bool Close(string key) => _open.TryRemove(key, out _);

    /// <summary>
    /// Closes the event and forgets who joined. Returns who that was.
    /// </summary>
    public IReadOnlyDictionary<ulong, string> End(string key)
    {
        _open.TryRemove(key, out _);

        return _joined.TryRemove(key, out var joined) ? joined : new Dictionary<ulong, string>();
    }

    /// <summary>
    /// The open event called <paramref name="key"/>, or with no key the only open event.
    /// </summary>
    public FindResult TryFind(string? key, [NotNullWhen(true)] out JoinPoint? point)
    {
        point = null;

        if (key is not null)
            return _open.TryGetValue(key, out point) ? FindResult.Found : FindResult.NotOpen;

        var open = Open;

        if (open.Count == 0)
            return FindResult.NoneOpen;

        if (open.Count > 1)
            return FindResult.Ambiguous;

        point = open[0];
        return FindResult.Found;
    }

    /// <summary>
    /// Signs the player up. False if they had already joined.
    /// </summary>
    public bool Join(string key, ulong guid, string name) =>
        _joined.GetOrAdd(key, static _ => new()).TryAdd(guid, name);

    public bool HasJoined(string key, ulong guid) =>
        _joined.TryGetValue(key, out var joined) && joined.ContainsKey(guid);

    /// <summary>
    /// Everyone who joined the event (open or closed) and hasn't been ended, by guid with their name.
    /// </summary>
    public IReadOnlyDictionary<ulong, string> Participants(string key) =>
        _joined.TryGetValue(key, out var joined) ? joined : new Dictionary<ulong, string>();
}
