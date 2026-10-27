using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Helpers.Abilities;

public sealed class CakeAbility(AbilityServices services) : ConsumableAbility(services)
{
    private const int DefaultSpawnEffectId = 21;

    public override bool Matches(ClientItemDefinition itemDefinition) =>
        _resourceManager.Consumables.Cakes.ContainsKey(itemDefinition.Id);

    public override bool HandleAbility(Player player, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition)
    {
        _resourceManager.Consumables.Cakes.TryGetValue(itemDefinition.Id, out var cakeDefinition);

        if (player.IsItemOnCooldown(itemDefinition.Id))
            return SendFailure(player);

        SpawnCakeNpc(player, cakeDefinition!);

        player.StartItemCooldown(itemDefinition.Id, cakeDefinition!.CooldownMs);
        player.StartActionBarCooldown(ActionBarId, slot, itemDefinition.Icon.Id, itemDefinition.NameId, clientItem.Count, cakeDefinition.CooldownMs);

        return true;
    }

    private void SpawnCakeNpc(Player player, CakeItemDefinition cakeDefinition)
    {
        var forwardDirection = Vector3.Transform(new Vector3(0, 0, 1), player.Rotation);
        var spawnPosition = new Vector4(
            player.Position.X + forwardDirection.X * 1.5f,
            player.Position.Y + forwardDirection.Y * 1.5f,
            player.Position.Z + forwardDirection.Z * 1.5f,
            player.Position.W
        );

        var cakeNpc = SpawnNpc(player, spawnPosition, npc =>
        {
            npc.NameId = cakeDefinition.NameId;
            npc.ModelId = cakeDefinition.ModelId;
            npc.TextureAlias = "";
            npc.TintAlias = "";
            npc.Scale = 1.0f;
            npc.Animation = cakeDefinition.Animation;
            npc.HideNamePlate = false;
            npc.IsInteractable = true;
            npc.CursorId = (byte)cakeDefinition.CursorId;
        });

        if (cakeNpc is null)
            return;

        var interactReadyTime = DateTimeOffset.MinValue;

        if (cakeDefinition.Type == CakeItemType.GiftCake)
        {
            cakeNpc.InteractAction = player =>
            {
                if (DateTimeOffset.UtcNow < interactReadyTime)
                    return;

                if (player.Items.Any(item => item.Definition == cakeDefinition.GrantItemId))
                {
                    player.SendTunneled(new ChatPacketDebugChat
                    {
                        Message = "<font color=\"#FFFF00\">You already have a slice - eat it before taking another.</font>",
                        PrintToChat = true
                    });
                    return;
                }

                interactReadyTime = DateTimeOffset.UtcNow.AddMilliseconds(cakeDefinition.InteractCooldownMs);

                _rewardManager.TryGrantItem(player, cakeDefinition.GrantItemId, 0, cakeDefinition.GrantItemQuantity, cakeNpc.Guid);
            };
        }
        else if (cakeDefinition.Type == CakeItemType.BossCake)
        {
            var lastTransform = -1;

            cakeNpc.InteractAction = player =>
            {
                if (DateTimeOffset.UtcNow < interactReadyTime)
                    return;

                interactReadyTime = DateTimeOffset.UtcNow.AddMilliseconds(cakeDefinition.InteractCooldownMs);

                lastTransform = RollExcluding(cakeDefinition.TransformAbilityIds.Length, lastTransform);
                var abilityId = cakeDefinition.TransformAbilityIds[lastTransform];

                ApplyTransformOrFoodEffect(player, abilityId, cakeDefinition.NameId);
            };
        }
        else if (cakeDefinition.Type == CakeItemType.PartyCrate)
        {
            var supplied = new HashSet<ulong>();

            cakeNpc.InteractAction = player =>
            {
                // The burst is shared, but the supplies aren't: a crowd clicking at once all get theirs.
                if (DateTimeOffset.UtcNow >= interactReadyTime)
                {
                    interactReadyTime = DateTimeOffset.UtcNow.AddMilliseconds(cakeDefinition.InteractCooldownMs);
                    PlayBurst(cakeNpc, cakeDefinition);
                }

                if (string.IsNullOrEmpty(cakeDefinition.RewardTableKey))
                    return;

                if (!supplied.Add(player.Guid))
                {
                    player.SendTunneled(new ChatPacketDebugChat
                    {
                        Message = "<font color=\"#FFFF00\">You already took your party supplies from this crate.</font>",
                        PrintToChat = true
                    });
                    return;
                }

                // A failed grant (full bag, bad table) leaves the roll for another click.
                if (!_rewardManager.TryRollReward(cakeDefinition.RewardTableKey, out var drop) || drop is null
                    || !_rewardManager.TryGrantReward(player, drop, cakeNpc.Guid))
                    supplied.Remove(player.Guid);
            };
        }
        else
        {
            var lastRoll = -1;

            cakeNpc.InteractAction = player =>
            {
                if (DateTimeOffset.UtcNow < interactReadyTime)
                    return;

                interactReadyTime = DateTimeOffset.UtcNow.AddMilliseconds(cakeDefinition.InteractCooldownMs);

                // Every scare group and transform is equally likely, except the one that played last.
                var roll = RollExcluding(cakeDefinition.ScareGroups.Length + cakeDefinition.TransformAbilityIds.Length, lastRoll);
                lastRoll = roll;

                if (roll < cakeDefinition.ScareGroups.Length)
                {
                    var scareEffects = cakeDefinition.ScareGroups[roll];

                    for (var i = 0; i < scareEffects.Length; i++)
                    {
                        player.SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect
                        {
                            Guid = cakeNpc.Guid,
                            CompositeEffectId = scareEffects[i],
                            Position = cakeNpc.Position,
                            Clear = i == 0
                        }, true);
                    }
                }
                else
                {
                    var abilityId = cakeDefinition.TransformAbilityIds[roll - cakeDefinition.ScareGroups.Length];

                    ApplyTransformOrFoodEffect(player, abilityId, cakeDefinition.NameId);
                }
            };
        }

        var spawnRecipients = BroadcastSpawn(player, cakeNpc, spawnPosition, cakeDefinition.SpawnEffectIds.Length > 0 ? cakeDefinition.SpawnEffectIds[0] : DefaultSpawnEffectId);

        for (var i = 1; i < cakeDefinition.SpawnEffectIds.Length; i++)
        {
            var spawnEffect = new PlayerUpdatePacketPlayCompositeEffect
            {
                Guid = cakeNpc.Guid,
                CompositeEffectId = cakeDefinition.SpawnEffectIds[i],
                Position = spawnPosition,
                Clear = false
            };

            foreach (var recipient in spawnRecipients)
                recipient.SendTunneled(spawnEffect);
        }

        var despawnTime = DateTimeOffset.UtcNow.AddMilliseconds(cakeDefinition.LifetimeMs);
        var nextOneShotTime = NextOneShotTime(cakeDefinition);
        DateTimeOffset? oneShotEndTime = null;
        var nextBurstTime = DateTimeOffset.UtcNow.AddMilliseconds(cakeDefinition.BurstIntervalMs);

        cakeNpc.UpdateEverySecondAction = () =>
        {
            var now = DateTimeOffset.UtcNow;

            if (now >= despawnTime)
            {
                DespawnNpc(cakeNpc, 0);
                return;
            }

            if (cakeDefinition.BurstEffectIds.Length > 0 && now >= nextBurstTime)
            {
                PlayBurst(cakeNpc, cakeDefinition);
                nextBurstTime = now.AddMilliseconds(cakeDefinition.BurstIntervalMs);
            }

            if (cakeDefinition.OneShotAnimation == 0)
                return;

            if (oneShotEndTime is not null)
            {
                if (now < oneShotEndTime)
                    return;

                SetCakeAnimation(cakeNpc, cakeDefinition.Animation);
                oneShotEndTime = null;
                nextOneShotTime = NextOneShotTime(cakeDefinition);
            }
            else if (now >= nextOneShotTime)
            {
                SetCakeAnimation(cakeNpc, cakeDefinition.OneShotAnimation);
                oneShotEndTime = now.AddMilliseconds(cakeDefinition.OneShotAnimationMs);
            }
        };
    }

    private static DateTimeOffset NextOneShotTime(CakeItemDefinition cakeDefinition) =>
        DateTimeOffset.UtcNow.AddMilliseconds(cakeDefinition.OneShotIntervalMs + Random.Shared.Next(cakeDefinition.OneShotIntervalMs + 1));

    private static void PlayBurst(Npc cakeNpc, CakeItemDefinition cakeDefinition)
    {
        if (cakeDefinition.BurstEffectIds.Length == 0)
            return;

        var packet = new PlayerUpdatePacketPlayCompositeEffect
        {
            Guid = cakeNpc.Guid,
            CompositeEffectId = cakeDefinition.BurstEffectIds[Random.Shared.Next(cakeDefinition.BurstEffectIds.Length)],
            Position = cakeNpc.Position,
            Clear = false
        };

        foreach (var viewer in cakeNpc.VisiblePlayers.Values)
            viewer.SendTunneled(packet);
    }

    private static void SetCakeAnimation(Npc cakeNpc, int animationId)
    {
        var packet = new PlayerUpdatePacketSetAnimation
        {
            Guid = cakeNpc.Guid,
            AnimationId = animationId,
            Flags = 1
        };

        foreach (var viewer in cakeNpc.VisiblePlayers.Values)
            viewer.SendTunneled(packet);
    }
}
