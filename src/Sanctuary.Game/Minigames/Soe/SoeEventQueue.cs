using System;
using System.Collections.Generic;

namespace Sanctuary.Game.Minigames.Soe;

/// <summary>
/// Timed events of a <see cref="SoeGameServer"/>. Port of the movies' com.sonyonline.game.SoeEventQueue: the movie
/// fires the soonest event with setInterval; here <see cref="Update"/> fires every event that's due, soonest first.
/// </summary>
public sealed class SoeEventQueue
{
    private sealed class QueuedEvent
    {
        public int Id;
        public string Type = string.Empty;
        public long Expire;
        public long Repeat;
        public object? Data;
    }

    private readonly Func<long> _clock;
    private readonly List<QueuedEvent> _events = [];

    private int _nextId;
    private long _pauseStart;

    public SoeEventQueue(Func<long> clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// An event came due: its id, type and data.
    /// </summary>
    public Action<int, string, object?>? OnEvent { get; set; }

    public int Count => _events.Count;

    public bool IsPaused => _pauseStart != 0;

    public void ClearAll()
    {
        _nextId = 0;
        _events.Clear();
        _pauseStart = 0;
    }

    /// <summary>
    /// Queues an event <paramref name="timeout"/> ms from now; a recurring one comes back every timeout.
    /// </summary>
    public int AddEvent(string type, long timeout, bool recur = false, object? data = null)
    {
        var queuedEvent = new QueuedEvent
        {
            Id = ++_nextId,
            Type = type,
            Expire = _clock() + timeout,
            Repeat = recur ? timeout : 0,
            Data = data
        };

        _events.Add(queuedEvent);
        Sort();

        return queuedEvent.Id;
    }

    public bool CancelEvent(int id)
    {
        return _events.RemoveAll(x => x.Id == id) > 0;
    }

    /// <summary>
    /// Fires an event now (the soonest without an id).
    /// </summary>
    public bool TriggerEvent(int? id = null)
    {
        var index = id is null ? (_events.Count > 0 ? 0 : -1) : _events.FindIndex(x => x.Id == id);

        if (index < 0)
            return false;

        var queuedEvent = _events[index];

        if (queuedEvent.Repeat != 0)
        {
            queuedEvent.Expire = _clock() + queuedEvent.Repeat;
            Sort();
        }
        else
        {
            _events.RemoveAt(index);
        }

        OnEvent?.Invoke(queuedEvent.Id, queuedEvent.Type, queuedEvent.Data);

        return true;
    }

    public void Pause()
    {
        if (!IsPaused)
            _pauseStart = Math.Max(1, _clock());
    }

    public void Resume()
    {
        if (!IsPaused)
            return;

        var paused = _clock() - _pauseStart;
        _pauseStart = 0;

        foreach (var queuedEvent in _events)
            queuedEvent.Expire += paused;
    }

    /// <summary>
    /// Fires the events that are due. An event's handler may queue or cancel others.
    /// </summary>
    public void Update()
    {
        // Bounded, so a recurring event with no timeout can't spin forever.
        for (var fired = 0; fired < 64 && !IsPaused && _events.Count > 0 && _events[0].Expire <= _clock(); fired++)
            TriggerEvent();
    }

    private void Sort()
    {
        // Stable, like the movie's Array.sort on expiry for equal times in practice: earlier-queued first.
        var ordered = new List<QueuedEvent>(_events);
        ordered.Sort((a, b) => a.Expire != b.Expire ? a.Expire.CompareTo(b.Expire) : a.Id.CompareTo(b.Id));

        _events.Clear();
        _events.AddRange(ordered);
    }
}
