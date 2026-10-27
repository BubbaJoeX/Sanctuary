using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Lua;

namespace Sanctuary.Scripting;

/// <summary>
/// The <c>after</c>, <c>every</c> and <c>cancel</c> globals of a <see cref="ScriptSandbox"/>. Timers count
/// the sandbox's one-second ticks, so a delay is rounded up to whole seconds.
/// </summary>
internal sealed class ScriptTimers
{
    public const int MaxTimers = 64;

    private const int MaxSeconds = 24 * 60 * 60;

    private sealed class Timer
    {
        public required int Id { get; init; }
        public required LuaValue Callback { get; init; }
        public required int Interval { get; init; }
        public required bool Repeats { get; init; }
        public int Remaining { get; set; }
    }

    private readonly List<Timer> _timers = [];
    private int _nextId;

    public int Count
    {
        get
        {
            lock (_timers)
                return _timers.Count;
        }
    }

    public void Bind(LuaTable environment)
    {
        environment["after"] = new LuaFunction("after", (context, cancellationToken) =>
            new ValueTask<int>(context.Return(Add(context, false))));

        environment["every"] = new LuaFunction("every", (context, cancellationToken) =>
            new ValueTask<int>(context.Return(Add(context, true))));

        environment["cancel"] = new LuaFunction("cancel", (context, cancellationToken) =>
            new ValueTask<int>(context.Return(Cancel(context.GetArgument<int>(0)))));
    }

    private int Add(LuaFunctionExecutionContext context, bool repeats)
    {
        var seconds = context.GetArgument<double>(0);
        var callback = context.Arguments[1];

        if (callback.Type != LuaValueType.Function)
            throw new LuaRuntimeException(context.State, "Timers need a function to call.");

        if (!(seconds > 0 && seconds <= MaxSeconds))
            throw new LuaRuntimeException(context.State, $"Timers wait between 1 second and {MaxSeconds} seconds.");

        var interval = Math.Max(1, (int)Math.Ceiling(seconds));

        lock (_timers)
        {
            if (_timers.Count >= MaxTimers)
                throw new LuaRuntimeException(context.State, $"A script has at most {MaxTimers} timers.");

            var timer = new Timer
            {
                Id = ++_nextId,
                Callback = callback,
                Interval = interval,
                Repeats = repeats,
                Remaining = interval
            };

            _timers.Add(timer);
            return timer.Id;
        }
    }

    public bool Cancel(int id)
    {
        lock (_timers)
            return _timers.RemoveAll(timer => timer.Id == id) > 0;
    }

    /// <summary>
    /// Counts one second off every timer and returns the ones that came due, in the order they were made.
    /// One-shot timers are dropped and repeating ones start over.
    /// </summary>
    public List<(int Id, LuaValue Callback)> Advance()
    {
        var due = new List<(int, LuaValue)>();

        lock (_timers)
        {
            foreach (var timer in _timers)
            {
                if (--timer.Remaining > 0)
                    continue;

                due.Add((timer.Id, timer.Callback));
                timer.Remaining = timer.Repeats ? timer.Interval : 0;
            }

            _timers.RemoveAll(timer => timer.Remaining <= 0);
        }

        return due;
    }
}
