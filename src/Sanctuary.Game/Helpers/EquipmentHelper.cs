using System.Linq;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

/// <summary>
/// Putting an item on in one of a player's jobs: what the client's equip request does, for the server's own use too
/// (a fishing pond hands a Fisherman their rod).
/// </summary>
public static class EquipmentHelper
{
    /// <summary>
    /// Whether the job is ranked high enough for the item (its MinProfileRank), as Free Realms gated gear; tells
    /// the player why not. Admins wear anything, for testing.
    /// </summary>
    public static bool CheckRank(Player player, ClientPcProfile profile, ClientItemDefinition definition)
    {
        if (definition.MinProfileRank <= profile.Rank || player.IsAdmin)
            return true;

        ChatHelper.SendSystemMessage(player, $"You need to reach rank {definition.MinProfileRank} in this job to wear that (it's rank {profile.Rank}).");
        return false;
    }

    /// <summary>
    /// Wears the item in its slot of the profile. Returns the item it replaced there, or null.
    /// </summary>
    public static int? SetProfileItem(ClientPcProfile profile, ClientItem clientItem, ClientItemDefinition definition)
    {
        if (!profile.Items.TryGetValue(definition.Slot, out var profileItem))
        {
            profile.Items.Add(definition.Slot, new ProfileItem
            {
                Id = clientItem.Id,
                Slot = definition.Slot
            });

            return null;
        }

        var oldItemId = profileItem.Id;

        profileItem.Id = clientItem.Id;
        profileItem.Slot = definition.Slot;

        return oldItemId;
    }

    /// <summary>
    /// Saves the change <see cref="SetProfileItem"/> made. False if the character's profile or item is missing.
    /// </summary>
    public static bool SaveProfileItem(DatabaseContext dbContext, ulong playerGuid, int profileId, int? oldItemId, int itemId)
    {
        var characterId = GuidHelper.GetPlayerId(playerGuid);

        var dbProfile = dbContext.Profiles
            .Include(x => x.Items)
            .SingleOrDefault(x => x.CharacterId == characterId && x.Id == profileId);

        var dbItem = dbContext.Items.SingleOrDefault(x => x.CharacterId == characterId && x.Id == itemId);

        if (dbProfile is null || dbItem is null)
            return false;

        if (oldItemId is not null && dbProfile.Items.SingleOrDefault(x => x.Id == oldItemId) is { } dbItemOld)
            dbProfile.Items.Remove(dbItemOld);

        dbProfile.Items.Add(dbItem);

        return dbContext.SaveChanges() > 0;
    }

    /// <summary>
    /// Shows the player the item worn in <paramref name="slot"/> of the profile, and the players around them too when
    /// it's their active job.
    /// </summary>
    public static void SendEquipped(Player player, int profileId, int slot, ClientItem clientItem, ClientItemDefinition definition)
    {
        var clientUpdatePacketEquipItem = new ClientUpdatePacketEquipItem();

        clientUpdatePacketEquipItem.Guid = clientItem.Id;

        clientUpdatePacketEquipItem.Attachment.ModelName = definition.ModelName;
        clientUpdatePacketEquipItem.Attachment.TextureAlias = ItemDefinitionHelper.GetAttachmentTextureAlias(definition.TextureAlias, player.SkinTone);
        clientUpdatePacketEquipItem.Attachment.TintAlias = definition.TintAlias;
        clientUpdatePacketEquipItem.Attachment.TintId = clientItem.Tint == 0 ? definition.Icon.TintId : clientItem.Tint;
        clientUpdatePacketEquipItem.Attachment.CompositeEffectId = definition.CompositeEffectId;
        clientUpdatePacketEquipItem.Attachment.Slot = slot;

        clientUpdatePacketEquipItem.ProfileId = profileId;

        clientUpdatePacketEquipItem.Equip = true;

        player.SendTunneled(clientUpdatePacketEquipItem);

        var playerUpdatePacketEquipItemChange = new PlayerUpdatePacketEquipItemChange();

        playerUpdatePacketEquipItemChange.Guid = player.Guid;

        playerUpdatePacketEquipItemChange.Id = clientItem.Id;

        playerUpdatePacketEquipItemChange.Attachment = clientUpdatePacketEquipItem.Attachment;

        playerUpdatePacketEquipItemChange.ProfileId = player.ActiveProfileId;

        playerUpdatePacketEquipItemChange.ItemClassId = definition.Class;
        playerUpdatePacketEquipItemChange.WieldType = player.GetWieldType();

        if (profileId == player.ActiveProfileId)
            player.SendTunneledToVisible(playerUpdatePacketEquipItemChange);

        player.SendToolbar();

        player.RefreshWeaponFlair(profileId, slot);
    }
}
