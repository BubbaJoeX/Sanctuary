using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sanctuary.Scripting.Tests;

[TestClass]
public class ScriptSandboxTests
{
    private sealed class MockScriptPlayer : IScriptablePlayer
    {
        public ulong Guid => 42;
        public string Name => "Tester";
        public (float x, float y, float z) Position => (1, 2, 3);

        public List<string> Messages { get; } = [];
        public List<(int Item, int Quantity)> Given { get; } = [];

        public void Message(string message) => Messages.Add(message);
        public void GiveItem(int itemDefinitionId, int quantity) => Given.Add((itemDefinitionId, quantity));

        public List<int> AcceptedQuests { get; } = [];

        public void AcceptQuest(int questId) => AcceptedQuests.Add(questId);

        public bool HasCompletedQuest(int questId) => questId == 7;

        public bool IsGm => false;
        public bool IsMounted { get; private set; } = true;

        public void Dismount() => IsMounted = false;

        public (float X, float Y, float Z, float? Heading)? TeleportedTo { get; private set; }

        public void Teleport(float x, float y, float z, float? heading) => TeleportedTo = (x, y, z, heading);

        public int Coins { get; private set; }

        public void GiveCoins(int amount) => Coins += amount;

        public void Heal(int amount) { }

        public List<int> Effects { get; } = [];

        public void PlayEffect(int compositeEffectId) => Effects.Add(compositeEffectId);

        public void Emote(int animationId) { }

        public (int Model, int Seconds)? Transformed { get; private set; }

        public void Transform(int modelId, int seconds) => Transformed = (modelId, seconds);

        public void ClearTransform() => Transformed = null;

        public (float Scale, int Seconds)? Resized { get; private set; }

        public void Resize(float scale, int seconds) => Resized = (scale, seconds);
    }

    private static MockScriptNpc CreateNpc() => new(new MockScriptZone(NullLogger.Instance));

    [TestMethod]
    public async Task Hooks_ReceivePlayerAndUseApi()
    {
        var npc = CreateNpc();
        var player = new MockScriptPlayer();

        var (sandbox, error) = await ScriptSandbox.CompileAsync(npc, """
            function onInteract(player)
                player:message("Hi " .. player.name)
                player:giveItem(7, 2)
                npc:emote(3333)
            end
            """, "test");

        Assert.IsNull(error);
        Assert.IsNotNull(sandbox);
        Assert.IsTrue(sandbox.HasHook("onInteract"));
        Assert.IsFalse(sandbox.HasHook("onSecond"));

        Assert.IsNull(await sandbox.CallAsync("onInteract", player));

        CollectionAssert.AreEqual(new[] { "Hi Tester" }, player.Messages);
        Assert.AreEqual((7, 2), player.Given[0]);
        Assert.AreEqual((3333, false), npc.Emotes[0]);
    }

    [TestMethod]
    public async Task UnsafeLibraries_AreNotAvailable()
    {
        foreach (var source in new[] { "os.exit()", "io.write('x')", "load('return 1')", "require('x')", "debug.traceback()" })
        {
            var (sandbox, error) = await ScriptSandbox.CompileAsync(CreateNpc(), source, "unsafe");

            Assert.IsNull(sandbox, source);
            Assert.IsNotNull(error, source);
        }
    }

    [TestMethod]
    public async Task EndlessLoop_IsStopped()
    {
        var (sandbox, error) = await ScriptSandbox.CompileAsync(CreateNpc(), "function onSecond() while true do end end", "loop");

        Assert.IsNull(error);
        Assert.IsNotNull(sandbox);

        var callError = await sandbox.CallAsync("onSecond");

        Assert.IsNotNull(callError);
        StringAssert.Contains(callError, "Stopped");
    }

    [TestMethod]
    public async Task CompileError_IsReported()
    {
        var (sandbox, error) = await ScriptSandbox.CompileAsync(CreateNpc(), "function onSpawn( end", "broken");

        Assert.IsNull(sandbox);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public async Task SandboxedLibraries_AreIsolated()
    {
        var npc = CreateNpc();

        var (first, _) = await ScriptSandbox.CompileAsync(npc, "string.upper = nil", "first");
        var (second, error) = await ScriptSandbox.CompileAsync(npc, "x = string.upper('a')", "second");

        Assert.IsNotNull(first);
        Assert.IsNull(error);
        Assert.IsNotNull(second);
    }

    private static async Task<ScriptSandbox> CompileAsync(IScriptableNpc npc, string source)
    {
        var (sandbox, error) = await ScriptSandbox.CompileAsync(npc, source, "test");

        Assert.IsNull(error, error);
        Assert.IsNotNull(sandbox);
        return sandbox;
    }

    [TestMethod]
    public async Task Timers_FireOnTheirTick()
    {
        var npc = CreateNpc();
        var sandbox = await CompileAsync(npc, """
            log = {}
            local ticker = every(1, function() table.insert(log, "every") end)
            after(2, function() table.insert(log, "after") cancel(ticker) end)
            local dropped = after(1, function() table.insert(log, "dropped") end)
            cancel(dropped)
            function onSecond() table.insert(log, "second") end
            function onInteract(player) player:message(table.concat(log, ",")) end
            """);

        Assert.IsTrue(sandbox.NeedsTick);

        for (var i = 0; i < 4; i++)
            Assert.IsNull(await sandbox.TickAsync());

        var player = new MockScriptPlayer();
        await sandbox.CallAsync("onInteract", player);

        Assert.AreEqual("second,every,second,every,after,second,second", player.Messages[0]);
    }

    [TestMethod]
    public async Task Timers_NeedTickWithoutOnSecond()
    {
        var sandbox = await CompileAsync(CreateNpc(), "after(1, function() end)");

        Assert.IsTrue(sandbox.NeedsTick);
        Assert.IsNull(await sandbox.TickAsync());
        Assert.IsFalse(sandbox.NeedsTick);
    }

    [TestMethod]
    public async Task FailingRepeatingTimer_IsCancelled()
    {
        var sandbox = await CompileAsync(CreateNpc(), "every(1, function() error('boom') end)");

        var error = await sandbox.TickAsync();

        Assert.IsNotNull(error);
        StringAssert.Contains(error, "boom");
        Assert.IsFalse(sandbox.NeedsTick);
    }

    [TestMethod]
    public async Task Timers_RejectBadArguments()
    {
        foreach (var source in new[] { "after(0, function() end)", "after(1, 5)", "for i = 1, 65 do every(1, function() end) end" })
        {
            var (sandbox, error) = await ScriptSandbox.CompileAsync(CreateNpc(), source, "bad");

            Assert.IsNull(sandbox, source);
            Assert.IsNotNull(error, source);
        }
    }

    [TestMethod]
    public async Task Prelude_HelpersWork()
    {
        var npc = CreateNpc();
        var player = new MockScriptPlayer();
        var sandbox = await CompileAsync(npc, """
            function onInteract(player)
                local list = shuffle({ 1, 2, 3 })
                player:message(string.format("%.0f", distance(player, { x = 4, z = 7 })))
                player:message(countdown(75))
                player:message(tostring(#list == 3 and pick(list) ~= nil))
                player:message(tostring(chance(1)) .. tostring(chance(0)))
                player:message(tostring(os.time() > 0) .. tostring(os.execute == nil))
            end
            """);

        Assert.IsNull(await sandbox.CallAsync("onInteract", player));

        CollectionAssert.AreEqual(new[] { "5", "1:15", "true", "truefalse", "truetrue" }, player.Messages);
    }

    [TestMethod]
    public async Task PlayerApi_IsBoundAndLimited()
    {
        var player = new MockScriptPlayer();
        var sandbox = await CompileAsync(CreateNpc(), """
            function onInteract(player)
                if player.isMounted then player:dismount() end
                player:teleport(1, 2, 3, 1.5)
                player:giveCoins(250)
                player:playEffect(46)
                player:transform(1234, 30)
                player:resize(2, 10)
            end

            function onChoice(player, hook)
                if hook == "coins" then player:giveCoins(1000000) end
                if hook == "scale" then player:resize(50, 10) end
                if hook == "transform" then player:transform(1234, 0) end
            end
            """);

        Assert.IsNull(await sandbox.CallAsync("onInteract", player));

        Assert.IsFalse(player.IsMounted);
        Assert.AreEqual((1f, 2f, 3f, (float?)1.5f), player.TeleportedTo);
        Assert.AreEqual(250, player.Coins);
        CollectionAssert.AreEqual(new[] { 46 }, player.Effects);
        Assert.AreEqual((1234, 30), player.Transformed);
        Assert.AreEqual((2f, 10), player.Resized);

        foreach (var hook in new[] { "coins", "scale", "transform" })
            Assert.IsNotNull(await sandbox.CallAsync("onChoice", player, hook), hook);
    }

    [TestMethod]
    public async Task NpcApi_FindsPlayersAndSignals()
    {
        var npc = CreateNpc();
        var player = new MockScriptPlayer();
        npc.Players.Add(player);

        var sandbox = await CompileAsync(npc, """
            function onSecond()
                for _, other in ipairs(npc:getPlayers(10)) do
                    npc:sayTo(other, "psst " .. other.name)
                end

                local found = npc:getPlayer("42")
                found:message("found by text")
                npc:getPlayer(42):message("found by number")

                if npc:getPlayer("nope") == nil then
                    npc:signal("ring", 3, 25)
                end

                npc:setScale(2)
                npc:signal("home", tostring(npc.home.x))
            end
            """);

        Assert.IsNull(await sandbox.TickAsync());

        CollectionAssert.AreEqual(new[] { ((ulong)42, "psst Tester") }, npc.Whispers);
        CollectionAssert.AreEqual(new[] { "found by text", "found by number" }, player.Messages);
        CollectionAssert.AreEqual(new[] { ("ring", (string?)"3", 25f), ("home", "5", 0f) }, npc.Signals);
        Assert.AreEqual(2f, npc.Scale);
    }

    [TestMethod]
    public async Task Signals_ReachOnSignal()
    {
        var player = new MockScriptPlayer();
        var npc = CreateNpc();
        npc.Players.Add(player);

        var sandbox = await CompileAsync(npc, """
            function onSignal(name, data, from)
                npc:getPlayer(42):message(name .. "|" .. tostring(data) .. "|" .. from)
            end
            """);

        Assert.IsNull(await sandbox.CallSignalAsync("ring", null, 99));
        Assert.IsNull(await sandbox.CallSignalAsync("ring", "loud", 7));

        CollectionAssert.AreEqual(new[] { "ring|nil|99", "ring|loud|7" }, player.Messages);
    }

    [TestMethod]
    public async Task ZoneFlags_AreSharedBetweenScripts()
    {
        var zone = new MockScriptZone(NullLogger.Instance);
        var first = new MockScriptNpc(zone);
        var second = new MockScriptNpc(zone);
        var player = new MockScriptPlayer();

        var writer = await CompileAsync(first, """
            function onSecond()
                npc.zone:setFlag("door", "open")
                npc.zone:addFlag("clues")
                npc.zone:addFlag("clues", 2)
                npc.zone:setFlag("gone", true)
                npc.zone:setFlag("gone", nil)
            end
            """);

        var reader = await CompileAsync(second, """
            function onInteract(player)
                local zone = npc.zone
                player:message(zone:getFlag("door") .. " " .. zone:getFlag("clues") .. " " .. tostring(zone:getFlag("gone")))
            end

            function onChoice(player, hook)
                npc.zone:setFlag("bad", {})
            end
            """);

        Assert.IsNull(await writer.TickAsync());
        Assert.IsNull(await reader.CallAsync("onInteract", player));
        Assert.AreEqual("open 3 nil", player.Messages[0]);

        Assert.IsNotNull(await reader.CallAsync("onChoice", player, "x"));
    }
}
