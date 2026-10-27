using System;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Lua;

namespace Sanctuary.Scripting;

/// <summary>
/// A GM-authored script attached to one NPC or prop. It runs in its own environment with a whitelisted
/// standard library (see <see cref="ScriptRuntime.CreateSandboxEnv"/>), and every call is cut off after
/// <see cref="CallTimeout"/> so a runaway script can't stall the zone's runtime.
/// </summary>
/// <remarks>
/// Scripts define optional global hooks: onSpawn(), onSecond(), onInteract(player), onProximity(player),
/// onLeave(player), onArrive(), onHear(player, message), onChoice(player, hook) and
/// onSignal(name, data, fromGuid). The NPC is the global <c>npc</c>; <c>after</c>, <c>every</c> and
/// <c>cancel</c> schedule callbacks on the one-second tick, and <see cref="Prelude"/> adds a few helpers.
/// </remarks>
public sealed class ScriptSandbox
{
    public const int MaxSourceLength = 16 * 1024;

    public static readonly TimeSpan CallTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Helpers every script gets, written in Lua so they behave like the script's own code.
    /// </summary>
    private const string Prelude = """
        local function where(thing)
            if type(thing) == "userdata" then
                return thing.position
            end
            return thing
        end

        function distance(a, b)
            a, b = where(a), where(b)
            return math.sqrt((a.x - b.x) ^ 2 + (a.z - b.z) ^ 2)
        end

        function pick(list)
            return list[math.random(#list)]
        end

        function chance(probability)
            return math.random() < probability
        end

        function shuffle(list)
            for i = #list, 2, -1 do
                local j = math.random(i)
                list[i], list[j] = list[j], list[i]
            end
            return list
        end

        function countdown(seconds)
            seconds = math.max(0, math.floor(seconds))
            return string.format("%d:%02d", math.floor(seconds / 60), seconds % 60)
        end
        """;

    private readonly ScriptRuntime _runtime;
    private readonly LuaTable _environment;
    private readonly ScriptTimers _timers;

    public string Source { get; }

    private ScriptSandbox(ScriptRuntime runtime, LuaTable environment, ScriptTimers timers, string source)
    {
        _runtime = runtime;
        _environment = environment;
        _timers = timers;
        Source = source;
    }

    /// <summary>
    /// Compiles and runs the script's top level. Returns the sandbox, or the error to show the GM.
    /// </summary>
    public static async ValueTask<(ScriptSandbox? Sandbox, string? Error)> CompileAsync(IScriptableNpc npc, string source, string chunkName)
    {
        if (source.Length > MaxSourceLength)
            return (null, $"Scripts are at most {MaxSourceLength} characters.");

        var runtime = npc.ScriptRuntime;
        var environment = runtime.CreateSandboxEnv();

        var timers = new ScriptTimers();

        environment["npc"] = new LuaValue(NpcUserData.GetOrCreate(npc));
        environment["print"] = CreatePrint(npc.Logger, chunkName);
        timers.Bind(environment);

        try
        {
            await runtime.ExecuteSourceAsync(Prelude, "prelude", environment, CallTimeout);
            await runtime.ExecuteSourceAsync(source, chunkName, environment, CallTimeout);
        }
        catch (Exception ex) when (IsScriptError(ex))
        {
            return (null, Describe(ex));
        }

        return (new ScriptSandbox(runtime, environment, timers, source), null);
    }

    public bool HasHook(string name) => _environment[name].Type == LuaValueType.Function;

    /// <summary>
    /// The script has an onSecond hook or a timer waiting, so <see cref="TickAsync"/> has work to do.
    /// </summary>
    public bool NeedsTick => _timers.Count > 0 || HasHook("onSecond");

    /// <summary>
    /// Runs onSecond, then every timer that came due. A timer that fails is cancelled, so a broken
    /// <c>every</c> doesn't fail again each second. Returns the first error, or null.
    /// </summary>
    public async ValueTask<string?> TickAsync()
    {
        var error = await CallAsync("onSecond");

        foreach (var (id, callback) in _timers.Advance())
        {
            var timerError = await InvokeAsync(callback, []);

            if (timerError is null)
                continue;

            _timers.Cancel(id);
            error ??= $"timer {id}: {timerError}";
        }

        return error;
    }

    /// <summary>
    /// Runs onSignal(name, data, fromGuid) if the script defines it.
    /// </summary>
    public async ValueTask<string?> CallSignalAsync(string name, string? data, ulong fromGuid)
    {
        var function = _environment["onSignal"];

        if (function.Type != LuaValueType.Function)
            return null;

        return await InvokeAsync(function, [name, data is null ? LuaValue.Nil : data, fromGuid.ToString()]);
    }

    /// <summary>
    /// Calls a hook if the script defines it. Returns the error, or null if it ran (or isn't defined).
    /// </summary>
    public async ValueTask<string?> CallAsync(string hook, IScriptablePlayer? player = null, string? argument = null)
    {
        var function = _environment[hook];

        if (function.Type != LuaValueType.Function)
            return null;

        LuaValue[] args = (player, argument) switch
        {
            (null, null) => [],
            (not null, null) => [new LuaValue(new PlayerUserData(player))],
            (null, not null) => [argument],
            _ => [new LuaValue(new PlayerUserData(player!)), argument!]
        };

        return await InvokeAsync(function, args);
    }

    private async ValueTask<string?> InvokeAsync(LuaValue function, LuaValue[] args)
    {
        try
        {
            await _runtime.CallAsync(function, args, CallTimeout);
            return null;
        }
        catch (Exception ex) when (IsScriptError(ex))
        {
            return Describe(ex);
        }
    }

    private static bool IsScriptError(Exception ex) => ex is LuaParseException or LuaCompileException
        or LuaRuntimeException or LuaAssertionException or LuaCanceledException or OperationCanceledException;

    private static string Describe(Exception ex) => ex is LuaCanceledException or OperationCanceledException
        ? $"Stopped after {CallTimeout.TotalMilliseconds} ms (endless loop?)."
        : ex.Message;

    private static LuaFunction CreatePrint(ILogger logger, string chunkName) => new("print", (context, cancellationToken) =>
    {
        var builder = new StringBuilder();

        for (var i = 0; i < context.ArgumentCount; i++)
        {
            if (i > 0)
                builder.Append('\t');

            builder.Append(context.Arguments[i].ToString());
        }

        logger.LogInformation("[Lua {Chunk}] {Message}", chunkName, builder.ToString());
        return new ValueTask<int>(0);
    });
}
