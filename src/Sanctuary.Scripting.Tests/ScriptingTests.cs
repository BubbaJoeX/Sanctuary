using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sanctuary.Scripting.Tests;

[TestClass]
public class ScriptingTests
{
    private ServiceProvider _serviceProvider = null!;
    private ILogger _logger = null!;
    private ScriptManager _scriptManager = null!;

    [TestInitialize]
    public void Setup()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        services.AddSingleton<ScriptManager>();

        _serviceProvider = services.BuildServiceProvider();

        _logger = _serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Tests");
        _scriptManager = _serviceProvider.GetRequiredService<ScriptManager>();

        _scriptManager.Load();
    }

    [TestMethod]
    public async Task AllZoneScriptsValid()
    {
        var mockZone = new MockScriptZone(_logger);
        _scriptManager.GetOrCreateContext(mockZone, out var context);

        var zoneScriptsDirectory = Path.Combine(ScriptManager.BaseDirectory, "Zone");
        var luaFiles = Directory.GetFiles(zoneScriptsDirectory, "*.lua");

        var allSucceeded = true;
        foreach (var luaFile in luaFiles)
        {
            _logger.LogInformation("Loading zone script: {ScriptFilePath}", luaFile);
            var succeeded = await context.LoadScriptAsync(luaFile);
            if (!succeeded)
            {
                _logger.LogError("Failed to load zone script: {ScriptFilePath}", luaFile);
                allSucceeded = false;
            }
        }
        Assert.IsTrue(allSucceeded, "One or more zone scripts failed to load.");
    }

    [TestMethod]
    public async Task AllNpcScriptsValid()
    {
        var mockZone = new MockScriptZone(_logger);
        var mockNpc = new MockScriptNpc(mockZone);
        _scriptManager.GetOrCreateContext(mockNpc, out var context);

        var npcScriptsDirectory = Path.Combine(ScriptManager.BaseDirectory, "Npc");
        var luaFiles = Directory.GetFiles(npcScriptsDirectory, "*.lua");

        var allSucceeded = true;
        foreach (var luaFile in luaFiles)
        {
            _logger.LogInformation("Loading NPC script: {ScriptFilePath}", luaFile);
            var succeeded = await context.LoadScriptAsync(luaFile);
            if (!succeeded)
            {
                _logger.LogError("Failed to load NPC script: {ScriptFilePath}", luaFile);
                allSucceeded = false;
            }
        }
        Assert.IsTrue(allSucceeded, "One or more NPC scripts failed to load.");
    }

    [TestMethod]
    public async Task ZoneEvents_PassThePlayerAndValues()
    {
        var mockZone = new MockScriptZone(_logger);
        _scriptManager.GetOrCreateContext(mockZone, out var context);

        var scriptPath = Path.Combine(Path.GetTempPath(), $"zone-events-{Guid.NewGuid():N}.lua");
        await File.WriteAllTextAsync(scriptPath, """
            registerCallback("battleEnd", function(zone, player, battleId, won)
                player:message(zone.name .. " " .. battleId .. " " .. tostring(won))
            end)
            registerCallback("fishCaught", function(zone, player, kind, itemId)
                player:message(kind .. " " .. itemId)
            end)
            """);

        try
        {
            Assert.IsTrue(await context.LoadScriptAsync(scriptPath));
            Assert.IsTrue(context.HasCallbacks("battleEnd"));
            Assert.IsFalse(context.HasCallbacks("questComplete"));

            var player = new ScriptSandboxTests.MockScriptPlayer();
            context.FireEvent("battleEnd", player, 12, true);
            context.FireEvent("fishCaught", player, "fish", 3012);

            // The callbacks run in the background, one at a time.
            for (var waited = 0; player.Messages.Count < 2 && waited < 5000; waited += 20)
                await Task.Delay(20);

            CollectionAssert.AreEquivalent(new[] { $"{mockZone.Name} 12 true", "fish 3012" }, player.Messages);
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider.Dispose();
    }
}

