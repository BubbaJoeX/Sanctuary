using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

public static class MerchantHelper
{
    /// <summary>
    /// The entry for <paramref name="itemDefinitionId"/> in the list of the merchant the player is buying from.
    /// Merchants that have left, or that the player has walked away from, sell nothing.
    /// </summary>
    public static bool TryGetEntry(Player player, ulong merchantGuid, int itemDefinitionId, [MaybeNullWhen(false)] out MerchantList.Entry entry)
    {
        entry = null;

        return player.VisibleNpcs.TryGetValue(merchantGuid, out var merchant)
            && TryGetEntry(merchant, player, itemDefinitionId, out entry);
    }

    /// <summary>
    /// The entry for <paramref name="itemDefinitionId"/> at any merchant in the player's reach.
    /// </summary>
    public static bool TryGetEntryInReach(Player player, int itemDefinitionId, [MaybeNullWhen(false)] out MerchantList.Entry entry)
    {
        foreach (var npc in player.VisibleNpcs.Values)
        {
            if (TryGetEntry(npc, player, itemDefinitionId, out entry))
                return true;
        }

        entry = null;
        return false;
    }

    private static bool TryGetEntry(Npc npc, Player player, int itemDefinitionId, [MaybeNullWhen(false)] out MerchantList.Entry entry)
    {
        entry = npc.MerchantList?.Entries.FirstOrDefault(x => x.ItemDefinitionId == itemDefinitionId && x.CanBuy);

        return entry is not null && InteractionMenuHelper.CanInteract(npc, player);
    }
}
