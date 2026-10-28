using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Authoring;
using Sanctuary.Scripting;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class ScriptTemplateTests
{
    private sealed class StubNpc : IScriptableNpc
    {
        public ulong Guid => 1;
        public string? Name { get; set; } = "Stub";
        public IScriptableZone Zone { get; } = new StubZone();
        public (float x, float y, float z) Position => (0, 0, 0);
        public List<ulong> NearbyGuids { get; } = [];
        public ILogger Logger => NullLogger.Instance;
        public ScriptRuntime ScriptRuntime { get; } = new(NullLogger.Instance);

        public ScriptContext GetOrCreateScriptContext() => throw new NotSupportedException();
        public bool TryAddScript(string scriptName) => false;
        public bool TryRemoveScript(string scriptName) => false;
        public void Say(string message) { }
        public void SayLocalized(int stringId) { }
        public void MoveTo(float x, float y, float z, bool direct) { }
        public void Emote(int animationId, bool loop) { }
        public void Face(IScriptablePlayer player) { }
        public void StartConversation(IScriptablePlayer player, int? nodeId) { }
        public void SetCollidable(bool collidable) { }
        public bool IsMoving => false;
        public void SetHeading(float heading) { }
        public void SetSpeed(float speed) { }
        public IReadOnlyList<ulong> GetNearbyPlayerGuids(float radius) => NearbyGuids;
        public void PlayAnimation(int animationId) { }
        public void SetBaseAnimation(int animationId) { }
        public IReadOnlyList<ulong> GetPlayerGuidsOnTop(float horizontalRadius, float minHeight, float maxHeight) => NearbyGuids;
        public int AddEffectTag(int compositeEffectId) => 1;
        public void RemoveEffectTag(int tagId) { }
        public void PlayEffect(int compositeEffectId) { }
        public void Remove(int compositeEffectId) { }
        public (float x, float y, float z) HomePosition => (0, 0, 0);
        public List<IScriptablePlayer> Players { get; } = [];
        public IReadOnlyList<IScriptablePlayer> GetNearbyPlayers(float radius) => Players;
        public IScriptablePlayer? GetPlayer(ulong guid) => Players.Find(player => player.Guid == guid);
        public void SayTo(IScriptablePlayer player, string message) { }
        public void SetScale(float scale) { }
        public void Rename(string name) { }
        public bool Signal(string name, string? data, float radius) => true;
        public bool IsMob => false;
        public int Health => 0;
        public int MaxHealth => 0;
        public bool IsDead => false;
        public ulong TargetGuid => 0;
        public int Hurt(IScriptablePlayer attacker, int damage) => 0;
        public int Attack(IScriptablePlayer player, int damage) => 0;
    }

    private sealed class StubZone : IScriptableZone
    {
        public IScriptManager ScriptManager => null!;
        public int Id => 1;
        public string Name => "stub";
        public ILogger Logger => NullLogger.Instance;
        public ScriptRuntime ScriptRuntime => throw new NotSupportedException();

        public ScriptContext GetOrCreateScriptContext() => throw new NotSupportedException();
        public bool TryAddScript(string scriptName) => false;
        public bool TryRemoveScript(string scriptName) => false;
        public void SetClientAreaEnabled(int areaId, bool enabled) { }
        public void Broadcast(string message) { }
        public void BroadcastNear(string message, float x, float z, float radius) { }
        public bool IsSeasonActive(string seasonName) => false;
        public bool IsEventActive(string eventKey) => false;

        public bool TrySpawnNpc(int npcId, ulong? npcGuid, float x, float y, float z, float heading, float pitch, float roll, out IScriptableNpc npc)
        {
            npc = null!;
            return false;
        }

        public int PlayerCount => 0;
        public IReadOnlyList<IScriptablePlayer> GetPlayers() => [];
        public IScriptablePlayer? GetPlayer(ulong guid, string? name) => null;
    }

    private sealed class StubPlayer : IScriptablePlayer
    {
        public ulong Guid => 7;
        public string Name => "Tester";
        public (float x, float y, float z) Position { get; set; }

        public void Message(string message) { }
        public void GiveItem(int itemDefinitionId, int quantity) { }
        public void AcceptQuest(int questId) { }
        public bool HasCompletedQuest(int questId) => false;
        public bool IsGm => false;
        public bool IsMounted => false;
        public void Dismount() { }
        public void Teleport(float x, float y, float z, float? heading) => Position = (x, y, z);
        public void GiveCoins(int amount) { }
        public void Heal(int amount) { }
        public void PlayEffect(int compositeEffectId) { }
        public void Emote(int animationId) { }
        public void Transform(int modelId, int seconds) { }
        public void ClearTransform() { }
        public void Resize(float scale, int seconds) { }
        public int ZoneId => 1;
        public string ZoneName => "stub";
        public int Health => 100;
        public int MaxHealth => 100;
        public bool IsKnockedOut => false;
        public bool Revive(int healthPercent) => false;
        public bool IsInCombat => false;
        public int Job => 1;
        public int JobRank => 1;
        public int Coins => 0;
        public int GroupId => 0;
        public bool IsGroupLeader => false;
        public IReadOnlyList<ulong> GroupMemberGuids => [];
        public int CountItem(int itemDefinitionId) => 0;
        public bool TakeItem(int itemDefinitionId, int quantity) => false;
        public bool TakeCoins(int amount) => false;
        public bool GiveStars(int amount) => true;
        public bool Warp(int zoneId, (float x, float y, float z)? position, float? heading) => false;
    }

    private const string Sample = """
        -- @name sample
        -- @description Says things
        -- @param text string Hi there
        -- @param count int 3
        function onInteract(player) npc:say("{{text}} x{{count}}") end
        """;

    [TestMethod]
    public void Parse_ReadsHeader()
    {
        var template = ScriptTemplate.Parse(Sample, null, null, true);

        Assert.AreEqual("sample", template.Name);
        Assert.AreEqual("Says things", template.Description);
        Assert.AreEqual(2, template.Params.Count);
        Assert.AreEqual(new ScriptTemplateParam("text", "string", "Hi there"), template.Params[0]);
    }

    [TestMethod]
    public void Render_UsesDefaultsAndValues()
    {
        var template = ScriptTemplate.Parse(Sample, null, null, true);

        Assert.IsTrue(template.TryRender(new Dictionary<string, string> { ["count"] = "5" }, out var source, out _));
        StringAssert.Contains(source, "npc:say(\"Hi there x5\")");
    }

    [TestMethod]
    public void Render_EscapesStringsForLua()
    {
        var template = ScriptTemplate.Parse(Sample, null, null, true);

        Assert.IsTrue(template.TryRender(new Dictionary<string, string> { ["text"] = "a\") os.exit() --" }, out var source, out _));
        StringAssert.Contains(source, "a\\\") os.exit() --");
    }

    [TestMethod]
    public void Render_RejectsBadNumbersAndUnknownKeys()
    {
        var template = ScriptTemplate.Parse(Sample, null, null, true);

        Assert.IsFalse(template.TryRender(new Dictionary<string, string> { ["count"] = "lots" }, out _, out var numberError));
        Assert.IsNotNull(numberError);

        Assert.IsFalse(template.TryRender(new Dictionary<string, string> { ["nope"] = "1" }, out _, out var keyError));
        Assert.IsNotNull(keyError);
    }

    [TestMethod]
    public void Render_RejectsPlaceholdersWithoutParam()
    {
        // Lua compiles "{{hits}}" as a nested table, so it has to fail here rather than at the first click.
        var template = ScriptTemplate.Parse("local health = {{hits}}\nfunction onInteract(player) health = health - 1 end", null, null, false);

        Assert.IsFalse(template.TryRender(new Dictionary<string, string>(), out _, out var error));
        StringAssert.Contains(error, "{{hits}}");
    }

    [TestMethod]
    public void Render_FillsLoadedTemplateWithDefaults()
    {
        var template = ScriptTemplate.Parse(File.ReadAllText(Path.Combine(FindTemplatesDirectory(), "pinata.lua")), null, null, false);

        Assert.IsTrue(template.TryRender(new Dictionary<string, string>(), out var source, out var error), error);
        StringAssert.Contains(source, "local health = 10");
        Assert.IsFalse(source.Contains("{{"), "No placeholder should reach Lua.");
    }

    [TestMethod]
    public async Task ShippedTemplates_CompileWithDefaults()
    {
        var directory = FindTemplatesDirectory();
        var files = Directory.GetFiles(directory, "*.lua");

        Assert.IsTrue(files.Length >= 9, "Expected the starter templates.");

        foreach (var file in files)
        {
            var template = ScriptTemplate.Parse(File.ReadAllText(file), null, null, true);

            Assert.IsTrue(ScriptTemplate.IsValidName(template.Name), $"{file} has no valid @name.");
            Assert.IsTrue(template.TryRender(new Dictionary<string, string>(), out var source, out var renderError), $"{file}: {renderError}");

            var (sandbox, compileError) = await ScriptSandbox.CompileAsync(new StubNpc(), source, template.Name);

            Assert.IsNotNull(sandbox, $"{file}: {compileError}");
        }
    }

    [TestMethod]
    public async Task ShippedTemplates_RunEveryHook()
    {
        foreach (var file in Directory.GetFiles(FindTemplatesDirectory(), "*.lua"))
        {
            var template = ScriptTemplate.Parse(File.ReadAllText(file), null, null, true);
            Assert.IsTrue(template.TryRender(new Dictionary<string, string>(), out var source, out _));

            var npc = new StubNpc();
            var player = new StubPlayer();
            var (sandbox, compileError) = await ScriptSandbox.CompileAsync(npc, source, template.Name);
            Assert.IsNotNull(sandbox, $"{file}: {compileError}");

            async Task Run(string hook, IScriptablePlayer? who = null, string? argument = null)
            {
                var error = await sandbox.CallAsync(hook, who, argument);
                Assert.IsNull(error, $"{template.Name} {hook}: {error}");
            }

            await Run("onSpawn");

            // A player clicks, walks off and is away a while, then comes back step by step and stays.
            await Run("onInteract", player);
            await Run("onChoice", player, "right");
            await Run("onChoice", player, "wrong");
            await Run("onHear", player, "Howdy there");
            await Run("onHear", player, "something else");

            for (var second = 0; second < 200; second++)
            {
                var away = second < 40 ? 40f : Math.Max(0f, 40f - (second - 40));
                player.Position = (away, 0, 0);

                npc.NearbyGuids.Clear();
                npc.Players.Clear();
                if (away < 12)
                {
                    npc.NearbyGuids.Add(player.Guid);
                    npc.Players.Add(player);
                }

                if (away == 5)
                    await Run("onProximity", player);

                if (second == 30)
                    await Run("onLeave", player);

                if (second % 25 == 0)
                    await Run("onInteract", player);

                if (second % 20 == 10)
                    await Run("onArrive");

                if (second % 30 == 15)
                {
                    var signalError = await sandbox.CallSignalAsync("ring", "1", 99);
                    Assert.IsNull(signalError, $"{template.Name} onSignal: {signalError}");
                }

                var tickError = await sandbox.TickAsync();
                Assert.IsNull(tickError, $"{template.Name} tick {second}: {tickError}");
            }
        }
    }

    private static string FindTemplatesDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Resources", "ScriptTemplates");

            if (Directory.Exists(candidate))
                return candidate;
        }

        throw new DirectoryNotFoundException("Resources/ScriptTemplates");
    }
}
