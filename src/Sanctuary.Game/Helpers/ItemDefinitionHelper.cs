using System;
using System.Linq;

using Sanctuary.Core.IO;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

public static class ItemDefinitionHelper
{
    public const int PetItemType = 2;
    public const int PetClothingItemType = 3;
    public const int PetUtilityItemType = 4;
    public const int MountItemType = 19;

    /// <summary>
    /// Placeholder in an item's texture alias for the wearer's skin tone texture (skintone1, skintone3, ...),
    /// so one item can show bare skin that matches every character.
    /// </summary>
    public const string SkinToneToken = "<skintone>";

    private const int NoPurchasesLeftMaxStackSize = 0;

    /// <summary>
    /// The texture alias an attachment is sent with: <see cref="SkinToneToken"/> (any case) replaced with
    /// <paramref name="skinTone"/>. Aliases without the token, or a character without a skin tone, are unchanged.
    /// </summary>
    public static string GetAttachmentTextureAlias(string textureAlias, string? skinTone)
    {
        if (string.IsNullOrEmpty(textureAlias)
            || string.IsNullOrEmpty(skinTone)
            || !textureAlias.Contains(SkinToneToken, StringComparison.OrdinalIgnoreCase))
            return textureAlias;

        return textureAlias.Replace(SkinToneToken, skinTone, StringComparison.OrdinalIgnoreCase);
    }

    public static ClientItemDefinition GetPlayerItemDefinition(Player player, IResourceManager resourceManager, ClientItemDefinition clientItemDefinition)
    {
        if (clientItemDefinition.Type != MountItemType
            || clientItemDefinition.MaxStackSize <= NoPurchasesLeftMaxStackSize
            || player.Mounts.Count(mount => mount.Definition == clientItemDefinition.Param1) < clientItemDefinition.MaxStackSize
            || HasTintChoice(resourceManager, clientItemDefinition))
            return clientItemDefinition;

        var playerItemDefinition = clientItemDefinition.Clone();

        playerItemDefinition.MaxStackSize = NoPurchasesLeftMaxStackSize;

        return playerItemDefinition;
    }

    public static void SendItemDefinition(Player player, IResourceManager resourceManager, ClientItemDefinition clientItemDefinition)
    {
        using var writer = new PacketWriter();

        GetPlayerItemDefinition(player, resourceManager, clientItemDefinition).Serialize(writer);

        var playerUpdatePacketItemDefinitionRequest = new PlayerUpdatePacketItemDefinitionRequest
        {
            Id = clientItemDefinition.Id,
            Payload = writer.Buffer
        };

        player.SendTunneled(playerUpdatePacketItemDefinitionRequest);
    }

    private static bool HasTintChoice(IResourceManager resourceManager, ClientItemDefinition clientItemDefinition) =>
        resourceManager.CoinStoreItems.TryGetValue(clientItemDefinition.Id, out var coinStoreItem) && coinStoreItem.TintGroupId != 0;
}
