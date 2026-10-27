using System.Globalization;
using System.Threading.Tasks;

using Lua;

namespace Sanctuary.Scripting;

internal sealed class PlayerUserData(IScriptablePlayer player) : ILuaUserData
{
    private IScriptablePlayer _player => player;

    public LuaTable? Metatable { get; set; } = SharedMetatable;

    public IScriptablePlayer Player => _player;

    #region API

    private const int MaxGiveQuantity = 999;

    private static readonly LuaFunction MessageFunction = new("message", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<PlayerUserData>(0);
        var message = context.GetArgument<string>(1);

        self._player.Message(message);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction GiveItemFunction = new("giveItem", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<PlayerUserData>(0);
        var itemDefinitionId = context.GetArgument<int>(1);
        var quantity = 1;

        if (context.ArgumentCount > 2)
            quantity = context.GetArgument<int>(2);

        if (quantity is < 1 or > MaxGiveQuantity)
            throw new LuaRuntimeException(context.State, $"giveItem quantity must be between 1 and {MaxGiveQuantity}.");

        self._player.GiveItem(itemDefinitionId, quantity);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction AcceptQuestFunction = new("acceptQuest", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<PlayerUserData>(0);
        var questId = context.GetArgument<int>(1);

        self._player.AcceptQuest(questId);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction HasCompletedQuestFunction = new("hasCompletedQuest", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<PlayerUserData>(0);
        var questId = context.GetArgument<int>(1);

        return new ValueTask<int>(context.Return(self._player.HasCompletedQuest(questId)));
    });

    private const int MaxGiveCoins = 100_000;
    private const int MaxEffectSeconds = 3600;
    private const float MinScale = 0.25f;
    private const float MaxScale = 4f;

    private static readonly LuaFunction DismountFunction = new("dismount", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<PlayerUserData>(0);

        self._player.Dismount();

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction TeleportFunction = new("teleport", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<PlayerUserData>(0);
        var x = context.GetArgument<float>(1);
        var y = context.GetArgument<float>(2);
        var z = context.GetArgument<float>(3);
        float? heading = context.ArgumentCount > 4 ? context.GetArgument<float>(4) : null;

        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
            throw new LuaRuntimeException(context.State, "teleport needs real coordinates.");

        self._player.Teleport(x, y, z, heading);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction GiveCoinsFunction = new("giveCoins", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<PlayerUserData>(0);
        var amount = context.GetArgument<int>(1);

        if (amount is < 1 or > MaxGiveCoins)
            throw new LuaRuntimeException(context.State, $"giveCoins amount must be between 1 and {MaxGiveCoins}.");

        self._player.GiveCoins(amount);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction HealFunction = new("heal", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<PlayerUserData>(0);
        var amount = context.GetArgument<int>(1);

        self._player.Heal(amount);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction PlayEffectFunction = new("playEffect", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<PlayerUserData>(0);
        var compositeEffectId = context.GetArgument<int>(1);

        self._player.PlayEffect(compositeEffectId);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction EmoteFunction = new("emote", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<PlayerUserData>(0);
        var animationId = context.GetArgument<int>(1);

        self._player.Emote(animationId);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction TransformFunction = new("transform", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<PlayerUserData>(0);
        var modelId = context.GetArgument<int>(1);
        var seconds = context.GetArgument<int>(2);

        if (seconds is < 1 or > MaxEffectSeconds)
            throw new LuaRuntimeException(context.State, $"transform lasts between 1 and {MaxEffectSeconds} seconds.");

        self._player.Transform(modelId, seconds);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction ClearTransformFunction = new("clearTransform", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<PlayerUserData>(0);

        self._player.ClearTransform();

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction ResizeFunction = new("resize", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<PlayerUserData>(0);
        var scale = context.GetArgument<float>(1);
        var seconds = context.GetArgument<int>(2);

        if (!(scale >= MinScale && scale <= MaxScale))
            throw new LuaRuntimeException(context.State, $"resize scale must be between {MinScale} and {MaxScale}.");

        if (seconds is < 1 or > MaxEffectSeconds)
            throw new LuaRuntimeException(context.State, $"resize lasts between 1 and {MaxEffectSeconds} seconds.");

        self._player.Resize(scale, seconds);

        return new ValueTask<int>(context.Return());
    });

    #endregion

    private static readonly LuaTable SharedMetatable = BuildMetatable();

    private static LuaTable BuildMetatable()
    {
        var metatable = new LuaTable();

        metatable["__index"] = new LuaFunction("__index", static (context, cancellationToken) =>
        {
            var self = context.GetArgument<PlayerUserData>(0);
            var key = context.GetArgument<string>(1);

            var result = key switch
            {
                // Text, like every guid scripts see: a Lua number can't hold all 64 bits.
                "guid" => new LuaValue(self._player.Guid.ToString(CultureInfo.InvariantCulture)),
                "name" => new LuaValue(self._player.Name),
                "position" => new LuaValue(new LuaTable
                {
                    ["x"] = self._player.Position.x,
                    ["y"] = self._player.Position.y,
                    ["z"] = self._player.Position.z
                }),
                //
                "message" => MessageFunction,
                "giveItem" => GiveItemFunction,
                "acceptQuest" => AcceptQuestFunction,
                "hasCompletedQuest" => HasCompletedQuestFunction,
                "isGm" => new LuaValue(self._player.IsGm),
                "isMounted" => new LuaValue(self._player.IsMounted),
                "dismount" => DismountFunction,
                "teleport" => TeleportFunction,
                "giveCoins" => GiveCoinsFunction,
                "heal" => HealFunction,
                "playEffect" => PlayEffectFunction,
                "emote" => EmoteFunction,
                "transform" => TransformFunction,
                "clearTransform" => ClearTransformFunction,
                "resize" => ResizeFunction,
                _ => LuaValue.Nil
            };

            return new ValueTask<int>(context.Return(result));
        });

        return metatable;
    }
}
