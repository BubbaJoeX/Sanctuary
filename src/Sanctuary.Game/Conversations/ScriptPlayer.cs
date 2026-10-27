using System.Numerics;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Quests;
using Sanctuary.Game.Resources.Definitions.Rewards;
using Sanctuary.Packet;
using Sanctuary.Scripting;

namespace Sanctuary.Game.Conversations;

/// <summary>
/// Exposes a player to GM-authored scripts.
/// </summary>
public sealed class ScriptPlayer(Player player, IRewardManager rewardManager, IQuestManager questManager) : IScriptablePlayer
{
    public Player Player => player;

    public ulong Guid => player.Guid;
    public string Name => player.Name.FullName;
    public (float x, float y, float z) Position => (player.Position.X, player.Position.Y, player.Position.Z);

    public void Message(string message) => ChatHelper.SendSystemMessage(player, message);

    public void GiveItem(int itemDefinitionId, int quantity) => rewardManager.TryGrantItem(player, itemDefinitionId, 0, quantity);

    public void AcceptQuest(int questId) => questManager.AcceptQuest(player, questId);

    public bool HasCompletedQuest(int questId) => player.Quests.IsCompleted(questId);

    public bool IsGm => player.IsAdmin || player.IsMod;

    public bool IsMounted => player.Mount is not null;

    public void Dismount() => player.Dismount();

    public void Teleport(float x, float y, float z, float? heading)
    {
        var position = new Vector4(x, y, z, player.Position.W);
        var rotation = heading is { } value ? QuaternionExtensions.FromHeading(value) : player.Rotation;

        player.UpdatePosition(position, rotation);

        player.SendTunneled(new ClientUpdatePacketUpdateLocation
        {
            Position = position,
            Rotation = rotation,
            Teleport = true
        });
    }

    public void GiveCoins(int amount) => rewardManager.TryGrantCurrency(player, CurrencyType.Coins, amount);

    public void Heal(int amount) => player.Heal(amount);

    public void PlayEffect(int compositeEffectId) => player.SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect
    {
        Guid = player.Guid,
        CompositeEffectId = compositeEffectId,
        Position = player.Position
    }, true);

    public void Emote(int animationId) => player.SendTunneledToVisible(new PlayerUpdatePacketSetAnimation
    {
        Guid = player.Guid,
        AnimationId = animationId
    }, true);

    public void Transform(int modelId, int seconds) => player.ApplyTemporaryAppearance(modelId, seconds * 1000);

    public void ClearTransform() => player.RemoveTemporaryAppearance();

    public void Resize(float scale, int seconds) => player.ApplyTemporaryScale(scale, seconds * 1000);
}
