using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class ItemDefinitionHelperTests
{
    [TestMethod]
    public void GetAttachmentTextureAlias_ReplacesSkinToneToken()
    {
        Assert.AreEqual("swimsuit_skintone3", ItemDefinitionHelper.GetAttachmentTextureAlias("swimsuit_<skintone>", "skintone3"));
    }

    [TestMethod]
    public void GetAttachmentTextureAlias_IgnoresTokenCaseAndReplacesEveryToken()
    {
        Assert.AreEqual("skintone2-top-skintone2", ItemDefinitionHelper.GetAttachmentTextureAlias("<SkinTone>-top-<SKINTONE>", "skintone2"));
    }

    [TestMethod]
    public void GetAttachmentTextureAlias_LeavesAliasWithoutTokenUnchanged()
    {
        Assert.AreEqual("Armor-Plate-Blue", ItemDefinitionHelper.GetAttachmentTextureAlias("Armor-Plate-Blue", "skintone3"));
        Assert.AreEqual(string.Empty, ItemDefinitionHelper.GetAttachmentTextureAlias(string.Empty, "skintone3"));
    }

    [TestMethod]
    public void GetAttachmentTextureAlias_LeavesTokenWithoutSkinTone()
    {
        Assert.AreEqual("swimsuit_<skintone>", ItemDefinitionHelper.GetAttachmentTextureAlias("swimsuit_<skintone>", null));
        Assert.AreEqual("swimsuit_<skintone>", ItemDefinitionHelper.GetAttachmentTextureAlias("swimsuit_<skintone>", string.Empty));
    }
}
