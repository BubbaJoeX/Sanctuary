using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Quests;
using Sanctuary.Game.Resources.Definitions.Rewards;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Packet;
using Sanctuary.Scripting;

namespace Sanctuary.Game.Conversations;

/// <summary>
/// Exposes a player to GM-authored scripts.
/// </summary>
public sealed class ScriptPlayer(Player player, IRewardManager rewardManager, IQuestManager questManager,
    IResourceManager resourceManager) : IScriptablePlayer
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

    /// <summary>
    /// The zone's definition id, the one <see cref="Warp"/> takes.
    /// </summary>
    public int ZoneId => player.Zone.DefinitionId;
    public string ZoneName => player.Zone.Name;

    public int Health => player.Hitpoints;
    public int MaxHealth => player.MaxHitpoints;
    public bool IsKnockedOut => player.IsDead;
    public bool IsInCombat => player.IsInCombat;

    public int Job => player.ActiveProfileId;
    public int JobRank => player.ActiveProfile.Rank;

    public int Coins => player.Coins;

    public int GroupId => player.Group?.Id ?? 0;
    public bool IsGroupLeader => player.Group is { } group && group.LeaderGuid == player.Guid;
    public IReadOnlyList<ulong> GroupMemberGuids => player.Group?.Members ?? [];

    public int CountItem(int itemDefinitionId) =>
        player.Items.Where(item => item.Definition == itemDefinitionId).Sum(item => item.Count);

    public bool TakeItem(int itemDefinitionId, int quantity) => rewardManager.TryTakeItem(player, itemDefinitionId, quantity);

    public bool TakeCoins(int amount) => rewardManager.TryTakeCoins(player, amount);

    public bool GiveStars(int amount) => rewardManager.TryGrantExperience(player, player.ApplyExperienceBoost(amount));

    public bool Warp(int zoneId, (float x, float y, float z)? position, float? heading)
    {
        // Houses and combat zones need an owner or an encounter, so scripts only send players to world zones.
        if (!resourceManager.Zones.TryGetValue(zoneId, out var zoneDefinition) || zoneDefinition is not WorldZoneDefinition)
            return false;

        var destination = position is { } spot ? new Vector4(spot.x, spot.y, spot.z, 1f) : zoneDefinition.SpawnPosition;
        var rotation = heading is { } value ? QuaternionExtensions.FromHeading(value) : zoneDefinition.SpawnRotation;

        return player.TeleportToZone(zoneId, null, destination, rotation);
    }
}
