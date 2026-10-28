using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class MerchantHelperTests
{
    private const int SoldItemId = 9000008;
    private const int OtherItemId = 9000009;

    private static Npc CreateMerchant(Player player, ulong guid, params MerchantList.Entry[] entries)
    {
        var npc = new Npc(null!) { Guid = guid, InteractRange = 10 };
        npc.MerchantList = new MerchantList { Entries = [.. entries] };

        player.VisibleNpcs[guid] = npc;

        return npc;
    }

    private static MerchantList.Entry Sells(int itemDefinitionId, int tintGroupId = 0) =>
        new() { ItemDefinitionId = itemDefinitionId, AvailableTintGroupId = tintGroupId, CanBuy = true };

    [TestMethod]
    public void NamedMerchant_SellsItsOwnList()
    {
        var player = new Player(null!, null!, null!, null!);
        CreateMerchant(player, 1, Sells(SoldItemId, tintGroupId: 1));

        Assert.IsTrue(MerchantHelper.TryGetEntry(player, 1, SoldItemId, out var entry));
        Assert.AreEqual(1, entry.AvailableTintGroupId);

        Assert.IsFalse(MerchantHelper.TryGetEntry(player, 1, OtherItemId, out _));
    }

    [TestMethod]
    public void NamedMerchant_GoneOrUnknown_SellsNothing()
    {
        var player = new Player(null!, null!, null!, null!);
        CreateMerchant(player, 1, Sells(SoldItemId));

        Assert.IsFalse(MerchantHelper.TryGetEntry(player, 2, SoldItemId, out _));

        player.VisibleNpcs.TryRemove(1, out _);

        Assert.IsFalse(MerchantHelper.TryGetEntry(player, 1, SoldItemId, out _), "A merchant who has left sells nothing.");
    }

    [TestMethod]
    public void OutOfReach_SellsNothing()
    {
        var player = new Player(null!, null!, null!, null!);
        var merchant = CreateMerchant(player, 1, Sells(SoldItemId));

        merchant.SetPositionY(50);

        Assert.IsFalse(MerchantHelper.TryGetEntry(player, 1, SoldItemId, out _));
        Assert.IsFalse(MerchantHelper.TryGetEntryInReach(player, SoldItemId, out _));
    }

    [TestMethod]
    public void EntryThatCantBeBought_SellsNothing()
    {
        var player = new Player(null!, null!, null!, null!);
        CreateMerchant(player, 1, new MerchantList.Entry { ItemDefinitionId = SoldItemId, CanBuy = false });

        Assert.IsFalse(MerchantHelper.TryGetEntry(player, 1, SoldItemId, out _));
    }

    [TestMethod]
    public void InReach_FindsAnyMerchantSellingIt()
    {
        var player = new Player(null!, null!, null!, null!);
        CreateMerchant(player, 1, Sells(OtherItemId));
        CreateMerchant(player, 2, Sells(SoldItemId, tintGroupId: 2));
        player.VisibleNpcs[3] = new Npc(null!) { Guid = 3 };

        Assert.IsTrue(MerchantHelper.TryGetEntryInReach(player, SoldItemId, out var entry));
        Assert.AreEqual(2, entry.AvailableTintGroupId);
    }
}
