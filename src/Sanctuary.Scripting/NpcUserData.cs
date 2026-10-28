using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Lua;

namespace Sanctuary.Scripting;

internal sealed class NpcUserData(IScriptableNpc npc) : ILuaUserData
{
    private IScriptableNpc _npc => npc;

    public LuaTable? Metatable { get; set; } = SharedMetatable;

    // We weakly cache the wrappers so that we don't have to manually evict them when NPCs are unloaded.
    private static readonly ConditionalWeakTable<IScriptableNpc, NpcUserData> Cache = new();
    public static NpcUserData GetOrCreate(IScriptableNpc npc)
        => Cache.GetValue(npc, static n => new NpcUserData(n));

    #region API

    private static readonly LuaFunction SayFunction = new("say", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var message = context.GetArgument<string>(1);

        self._npc.Say(message);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction SayLocalizedFunction = new("sayLocalized", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var stringId = context.GetArgument<int>(1);

        self._npc.SayLocalized(stringId);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction MoveToFunction = new("moveTo", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var x = context.GetArgument<float>(1);
        var y = context.GetArgument<float>(2);
        var z = context.GetArgument<float>(3);
        var direct = false;

        if (context.ArgumentCount > 4)
        {
            direct = context.GetArgument<bool>(4);
        }

        self._npc.MoveTo(x, y, z, direct);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction EmoteFunction = new("emote", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var animationId = context.GetArgument<int>(1);
        var loop = context.ArgumentCount > 2 && context.GetArgument<bool>(2);

        self._npc.Emote(animationId, loop);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction GetNearbyPlayersFunction = new("getNearbyPlayers", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var radius = context.GetArgument<float>(1);

        return new ValueTask<int>(context.Return(ToGuidTable(self._npc.GetNearbyPlayerGuids(radius))));
    });

    private static readonly LuaFunction GetPlayersOnTopFunction = new("getPlayersOnTop", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var horizontalRadius = context.GetArgument<float>(1);
        var minHeight = context.GetArgument<float>(2);
        var maxHeight = context.GetArgument<float>(3);

        return new ValueTask<int>(context.Return(ToGuidTable(self._npc.GetPlayerGuidsOnTop(horizontalRadius, minHeight, maxHeight))));
    });

    private static readonly LuaFunction SetCollidableFunction = new("setCollidable", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var collidable = context.GetArgument<bool>(1);

        self._npc.SetCollidable(collidable);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction SetBaseAnimationFunction = new("setBaseAnimation", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var animationId = context.GetArgument<int>(1);

        self._npc.SetBaseAnimation(animationId);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction FaceFunction = new("face", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var player = context.GetArgument<PlayerUserData>(1);

        self._npc.Face(player.Player);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction StartConversationFunction = new("startConversation", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var player = context.GetArgument<PlayerUserData>(1);
        int? nodeId = context.ArgumentCount > 2 ? context.GetArgument<int>(2) : null;

        self._npc.StartConversation(player.Player, nodeId);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction AddEffectTagFunction = new("addEffectTag", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var compositeEffectId = context.GetArgument<int>(1);

        var tagId = self._npc.AddEffectTag(compositeEffectId);

        return new ValueTask<int>(context.Return(tagId));
    });

    private static readonly LuaFunction RemoveEffectTagFunction = new("removeEffectTag", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var tagId = context.GetArgument<int>(1);

        self._npc.RemoveEffectTag(tagId);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction SetSpeedFunction = new("setSpeed", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var speed = context.GetArgument<float>(1);

        self._npc.SetSpeed(speed);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction SetHeadingFunction = new("setHeading", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var heading = context.GetArgument<float>(1);

        self._npc.SetHeading(heading);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction PlayEffectFunction = new("playEffect", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var compositeEffectId = context.GetArgument<int>(1);

        self._npc.PlayEffect(compositeEffectId);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction RemoveFunction = new("remove", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var compositeEffectId = context.GetArgument<int>(1);

        self._npc.Remove(compositeEffectId);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction PlayAnimationFunction = new("playAnimation", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var animationId = context.GetArgument<int>(1);

        self._npc.PlayAnimation(animationId);

        return new ValueTask<int>(context.Return());
    });

    private const int MaxNameLength = 32;
    private const float MinScale = 0.1f;
    private const float MaxScale = 10f;

    private static readonly LuaFunction GetPlayersFunction = new("getPlayers", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var radius = context.GetArgument<float>(1);

        var players = new LuaTable();
        var index = 1;

        foreach (var player in self._npc.GetNearbyPlayers(radius))
            players[index++] = new LuaValue(new PlayerUserData(player));

        return new ValueTask<int>(context.Return(players));
    });

    private static readonly LuaFunction GetPlayerFunction = new("getPlayer", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var argument = context.Arguments[1];

        // Guids are handed out as text, but a small number is accepted too.
        ulong? guid = argument.TryRead<string>(out var text)
            ? ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : null
            : argument.TryRead<double>(out var number) && number >= 0 ? (ulong)number : null;

        var player = guid is { } value ? self._npc.GetPlayer(value) : null;

        return new ValueTask<int>(context.Return(player is null ? LuaValue.Nil : new LuaValue(new PlayerUserData(player))));
    });

    private static readonly LuaFunction SayToFunction = new("sayTo", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var player = context.GetArgument<PlayerUserData>(1);
        var message = context.GetArgument<string>(2);

        self._npc.SayTo(player.Player, message);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction SetScaleFunction = new("setScale", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var scale = context.GetArgument<float>(1);

        if (!(scale >= MinScale && scale <= MaxScale))
            throw new LuaRuntimeException(context.State, $"setScale must be between {MinScale} and {MaxScale}.");

        self._npc.SetScale(scale);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction RenameFunction = new("rename", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var name = context.GetArgument<string>(1).Trim();

        if (name.Length is 0 or > MaxNameLength)
            throw new LuaRuntimeException(context.State, $"rename needs 1 to {MaxNameLength} characters.");

        self._npc.Rename(name);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction ReturnHomeFunction = new("returnHome", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var home = self._npc.HomePosition;

        self._npc.MoveTo(home.x, home.y, home.z, false);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction SignalFunction = new("signal", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var name = context.GetArgument<string>(1);
        var data = context.ArgumentCount > 2 && context.Arguments[2].Type != LuaValueType.Nil ? context.Arguments[2].ToString() : null;
        var radius = context.ArgumentCount > 3 ? context.GetArgument<float>(3) : 0f;

        return new ValueTask<int>(context.Return(self._npc.Signal(name, data, radius)));
    });

    private const int MaxScriptDamage = 100_000;

    private static int ReadDamage(LuaFunctionExecutionContext context, string functionName)
    {
        var damage = context.GetArgument<int>(2);

        if (damage is < 1 or > MaxScriptDamage)
            throw new LuaRuntimeException(context.State, $"{functionName} damage must be between 1 and {MaxScriptDamage}.");

        return damage;
    }

    private static readonly LuaFunction HurtFunction = new("hurt", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var attacker = context.GetArgument<PlayerUserData>(1);
        var damage = ReadDamage(context, "hurt");

        return new ValueTask<int>(context.Return(self._npc.Hurt(attacker.Player, damage)));
    });

    private static readonly LuaFunction AttackFunction = new("attack", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var player = context.GetArgument<PlayerUserData>(1);
        var damage = ReadDamage(context, "attack");

        return new ValueTask<int>(context.Return(self._npc.Attack(player.Player, damage)));
    });

    #endregion

    private static LuaTable ToPositionTable((float x, float y, float z) position) => new()
    {
        ["x"] = position.x,
        ["y"] = position.y,
        ["z"] = position.z
    };

    private static LuaTable ToGuidTable(IEnumerable<ulong> guids)
    {
        var guidTable = new LuaTable();
        var index = 1;

        foreach (var guid in guids)
            guidTable[index++] = guid.ToString(CultureInfo.InvariantCulture);

        return guidTable;
    }

    private static readonly LuaTable SharedMetatable = BuildMetatable();

    private static LuaTable BuildMetatable()
    {
        var metatable = new LuaTable();

        metatable["__index"] = new LuaFunction("__index", static (context, cancellationToken) =>
        {
            // Argument 0 is the NPC userdata being indexed; argument 1 is the key.
            var self = context.GetArgument<NpcUserData>(0);
            var key = context.GetArgument<string>(1);

            var result = key switch
            {
                "guid" => new LuaValue(self._npc.Guid.ToString(CultureInfo.InvariantCulture)),
                "name" => new LuaValue(self._npc.Name ?? ""),
                "zone" => new LuaValue(ZoneUserData.GetOrCreate(self._npc.Zone)),
                "position" => new LuaValue(ToPositionTable(self._npc.Position)),
                "home" => new LuaValue(ToPositionTable(self._npc.HomePosition)),
                //
                "say" => SayFunction,
                "sayLocalized" => SayLocalizedFunction,
                "moveTo" => MoveToFunction,
                "emote" => EmoteFunction,
                "face" => FaceFunction,
                "startConversation" => StartConversationFunction,
                "getNearbyPlayers" => GetNearbyPlayersFunction,
                "playAnimation" => PlayAnimationFunction,
                "getPlayersOnTop" => GetPlayersOnTopFunction,
                "setCollidable" => SetCollidableFunction,
                "setBaseAnimation" => SetBaseAnimationFunction,
                "addEffectTag" => AddEffectTagFunction,
                "removeEffectTag" => RemoveEffectTagFunction,
                "playEffect" => PlayEffectFunction,
                "setHeading" => SetHeadingFunction,
                "setSpeed" => SetSpeedFunction,
                "isMoving" => new LuaValue(self._npc.IsMoving),
                "remove" => RemoveFunction,
                "getPlayers" => GetPlayersFunction,
                "getPlayer" => GetPlayerFunction,
                "sayTo" => SayToFunction,
                "setScale" => SetScaleFunction,
                "rename" => RenameFunction,
                "returnHome" => ReturnHomeFunction,
                "signal" => SignalFunction,
                "isMob" => new LuaValue(self._npc.IsMob),
                "health" => new LuaValue(self._npc.Health),
                "maxHealth" => new LuaValue(self._npc.MaxHealth),
                "isDead" => new LuaValue(self._npc.IsDead),
                "target" => self._npc.TargetGuid == 0 ? LuaValue.Nil : new LuaValue(self._npc.TargetGuid.ToString(CultureInfo.InvariantCulture)),
                "hurt" => HurtFunction,
                "attack" => AttackFunction,
                _ => LuaValue.Nil
            };

            return new ValueTask<int>(context.Return(result));
        });

        return metatable;
    }
}
