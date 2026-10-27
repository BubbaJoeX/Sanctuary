using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Lua;

namespace Sanctuary.Scripting;

internal sealed class ZoneUserData(IScriptableZone zone) : ILuaUserData
{
    private IScriptableZone _zone => zone;

    public LuaTable? Metatable { get; set; } = SharedMetatable;

    // We weakly cache the wrappers so that we don't have to manually evict them when zones are unloaded.
    private static readonly ConditionalWeakTable<IScriptableZone, ZoneUserData> Cache = new();
    public static ZoneUserData GetOrCreate(IScriptableZone zone)
        => Cache.GetValue(zone, static z => new ZoneUserData(z));

    private static readonly LuaFunction SpawnNpcFunction = new("spawnNpc", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ZoneUserData>(0);

        var npcId = context.GetArgument<int>(1);
        var x = context.GetArgument<float>(2);
        var y = context.GetArgument<float>(3);
        var z = context.GetArgument<float>(4);
        var heading = context.GetArgument<float>(5);
        var pitch = 0f;
        var roll = 0f;

        if (context.ArgumentCount > 7)
        {
            pitch = context.GetArgument<float>(6);
            roll = context.GetArgument<float>(7);
        }

        if (!self._zone.TrySpawnNpc(npcId, null, x, y, z, heading, pitch, roll, out var npc))
        {
            return new ValueTask<int>(context.Return(LuaValue.Nil));
        }

        var userData = NpcUserData.GetOrCreate(npc);

        var handle = new LuaValue(userData);

        return new ValueTask<int>(context.Return(handle));
    });

    private static readonly LuaFunction SpawnNpcWithGuidFunction = new("spawnNpcWithGuid", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ZoneUserData>(0);

        var npcId = context.GetArgument<int>(1);
        var npcGuid = context.Arguments[2].TryRead<string>(out var guidText)
            ? ulong.Parse(guidText, NumberStyles.None, CultureInfo.InvariantCulture)
            : context.GetArgument<ulong>(2);
        var x = context.GetArgument<float>(3);
        var y = context.GetArgument<float>(4);
        var z = context.GetArgument<float>(5);
        var heading = context.GetArgument<float>(6);
        var pitch = 0f;
        var roll = 0f;

        if (context.ArgumentCount > 8)
        {
            pitch = context.GetArgument<float>(7);
            roll = context.GetArgument<float>(8);
        }

        if (!self._zone.TrySpawnNpc(npcId, npcGuid, x, y, z, heading, pitch, roll, out var npc))
        {
            return new ValueTask<int>(context.Return(LuaValue.Nil));
        }

        var userData = NpcUserData.GetOrCreate(npc);

        var handle = new LuaValue(userData);

        return new ValueTask<int>(context.Return(handle));
    });

    private static readonly LuaFunction SetClientAreaEnabledFunction = new("setClientAreaEnabled", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ZoneUserData>(0);
        var areaId = context.GetArgument<int>(1);
        var enabled = context.GetArgument<bool>(2);

        self._zone.SetClientAreaEnabled(areaId, enabled);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction BroadcastFunction = new("broadcast", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ZoneUserData>(0);
        var message = context.GetArgument<string>(1);

        self._zone.Broadcast(message);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction BroadcastNearFunction = new("broadcastNear", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ZoneUserData>(0);
        var message = context.GetArgument<string>(1);
        var x = context.GetArgument<float>(2);
        var z = context.GetArgument<float>(3);
        var radius = context.GetArgument<float>(4);

        self._zone.BroadcastNear(message, x, z, radius);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction IsSeasonActiveFunction = new("isSeasonActive", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ZoneUserData>(0);
        var seasonName = context.GetArgument<string>(1);

        return new ValueTask<int>(context.Return(self._zone.IsSeasonActive(seasonName)));
    });

    private static readonly LuaFunction GetPlayersFunction = new("getPlayers", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ZoneUserData>(0);

        var players = new LuaTable();
        var index = 1;

        foreach (var player in self._zone.GetPlayers())
            players[index++] = new LuaValue(new PlayerUserData(player));

        return new ValueTask<int>(context.Return(players));
    });

    private static readonly LuaFunction GetPlayerFunction = new("getPlayer", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ZoneUserData>(0);
        var argument = context.Arguments[1];

        // A guid (as text, or a small number), or else a player's name.
        var player = argument.TryRead<string>(out var text)
            ? ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var guid)
                ? self._zone.GetPlayer(guid, null)
                : self._zone.GetPlayer(0, text)
            : argument.TryRead<double>(out var number) && number >= 0
                ? self._zone.GetPlayer((ulong)number, null)
                : null;

        return new ValueTask<int>(context.Return(player is null ? LuaValue.Nil : new LuaValue(new PlayerUserData(player))));
    });

    #region Flags

    // Zone flags let the scripts on different NPCs and props share state, e.g. which clues a hunt has found.
    // They live as long as the zone and are not saved.

    private const int MaxFlags = 256;
    private const int MaxFlagKeyLength = 64;
    private const int MaxFlagTextLength = 256;

    private readonly ConcurrentDictionary<string, LuaValue> _flags = new();

    private static string ReadFlagKey(LuaFunctionExecutionContext context)
    {
        var key = context.GetArgument<string>(1);

        if (key.Length is 0 or > MaxFlagKeyLength)
            throw new LuaRuntimeException(context.State, $"Flag names are 1 to {MaxFlagKeyLength} characters.");

        return key;
    }

    private void StoreFlag(LuaFunctionExecutionContext context, string key, LuaValue value)
    {
        if (!_flags.ContainsKey(key) && _flags.Count >= MaxFlags)
            throw new LuaRuntimeException(context.State, $"A zone holds at most {MaxFlags} flags.");

        _flags[key] = value;
    }

    private static readonly LuaFunction GetFlagFunction = new("getFlag", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ZoneUserData>(0);
        var key = ReadFlagKey(context);

        return new ValueTask<int>(context.Return(self._flags.TryGetValue(key, out var value) ? value : LuaValue.Nil));
    });

    private static readonly LuaFunction SetFlagFunction = new("setFlag", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ZoneUserData>(0);
        var key = ReadFlagKey(context);
        var value = context.ArgumentCount > 2 ? context.Arguments[2] : LuaValue.Nil;

        switch (value.Type)
        {
            case LuaValueType.Nil:
                self._flags.TryRemove(key, out _);
                break;

            case LuaValueType.Boolean or LuaValueType.Number:
                self.StoreFlag(context, key, value);
                break;

            case LuaValueType.String when value.Read<string>().Length <= MaxFlagTextLength:
                self.StoreFlag(context, key, value);
                break;

            default:
                throw new LuaRuntimeException(context.State, $"Flags hold a number, a boolean or a string of at most {MaxFlagTextLength} characters.");
        }

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction AddFlagFunction = new("addFlag", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ZoneUserData>(0);
        var key = ReadFlagKey(context);
        var amount = context.ArgumentCount > 2 ? context.GetArgument<double>(2) : 1;

        var current = self._flags.TryGetValue(key, out var value) && value.TryRead<double>(out var number) ? number : 0;
        var total = current + amount;

        self.StoreFlag(context, key, total);

        return new ValueTask<int>(context.Return(total));
    });

    #endregion

    private static readonly LuaTable SharedMetatable = BuildMetatable();

    private static LuaTable BuildMetatable()
    {
        var metatable = new LuaTable();

        metatable["__index"] = new LuaFunction("__index", static (context, cancellationToken) =>
        {
            var self = context.GetArgument<ZoneUserData>(0);

            var key = context.GetArgument<string>(1);

            var result = key switch
            {
                "id" => new LuaValue(self._zone.Id),
                "name" => new LuaValue(self._zone.Name),
                "spawnNpc" => SpawnNpcFunction,
                "spawnNpcWithGuid" => SpawnNpcWithGuidFunction,
                "setClientAreaEnabled" => SetClientAreaEnabledFunction,
                "broadcast" => BroadcastFunction,
                "broadcastNear" => BroadcastNearFunction,
                "isSeasonActive" => IsSeasonActiveFunction,
                "getFlag" => GetFlagFunction,
                "setFlag" => SetFlagFunction,
                "addFlag" => AddFlagFunction,
                "playerCount" => new LuaValue(self._zone.PlayerCount),
                "getPlayers" => GetPlayersFunction,
                "getPlayer" => GetPlayerFunction,
                _ => LuaValue.Nil
            };

            return new ValueTask<int>(context.Return(result));
        });

        return metatable;
    }
}
