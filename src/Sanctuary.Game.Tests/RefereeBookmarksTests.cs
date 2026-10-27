using System;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class RefereeBookmarksTests
{
    [TestMethod]
    public void NormalizeName_JoinsWordsLowercase()
    {
        Assert.AreEqual("race-start", RefereeBookmarks.NormalizeName(["Race", "Start"]));
    }

    [TestMethod]
    public void NormalizeName_RejectsColonsAndLongNames()
    {
        Assert.IsNull(RefereeBookmarks.NormalizeName(["a:b"]));
        Assert.IsNull(RefereeBookmarks.NormalizeName([new string('x', RefereeBookmarks.MaxNameLength + 1)]));
        Assert.IsNull(RefereeBookmarks.NormalizeName([]));
    }

    [TestMethod]
    public void SetGetRemove_PersistAcrossInstances()
    {
        var path = Path.Combine(Path.GetTempPath(), $"referee-bookmarks-{Guid.NewGuid():N}.json");

        try
        {
            var bookmarks = new RefereeBookmarks(NullLogger.Instance, path);

            Assert.IsTrue(bookmarks.Set(new RefereeBookmark { Name = "stage", ZoneDefinitionId = 1, X = 10, Y = 2, Z = -5, RotationW = 1 }));
            Assert.IsTrue(bookmarks.Set(new RefereeBookmark { Name = "finish", ZoneDefinitionId = 1, X = 30 }));

            var reloaded = new RefereeBookmarks(NullLogger.Instance, path);

            Assert.IsTrue(reloaded.TryGet("stage", out var stage));
            Assert.AreEqual(10f, stage.X);
            Assert.AreEqual(-5f, stage.Z);
            CollectionAssert.AreEqual(new[] { "finish", "stage" }, reloaded.GetAll().Select(x => x.Name).ToArray());

            Assert.IsTrue(reloaded.Remove("stage"));
            Assert.IsFalse(reloaded.Remove("stage"));
            Assert.IsFalse(new RefereeBookmarks(NullLogger.Instance, path).TryGet("stage", out _));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_MissingFileIsEmpty()
    {
        var bookmarks = new RefereeBookmarks(NullLogger.Instance, Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json"));

        Assert.AreEqual(0, bookmarks.GetAll().Count);
    }
}
