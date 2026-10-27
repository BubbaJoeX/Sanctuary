using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Game.Quests;
using Sanctuary.Scripting;

namespace Sanctuary.Game.Conversations;

public class NpcContentManager : INpcContentManager
{
    private const float ProximityRange = 8f;

    /// <summary>
    /// Leaving proximity needs a little more distance than entering, so standing on the edge doesn't spam.
    /// </summary>
    private const float ProximityExitRange = 10f;

    private const float ConversationRange = 20f;

    private readonly ILogger _logger;
    private readonly IRewardManager _rewardManager;
    private readonly IQuestManager _questManager;
    private readonly IResourceManager _resourceManager;

    public NpcContentManager(ILogger<NpcContentManager> logger, IRewardManager rewardManager, IQuestManager questManager,
        IResourceManager resourceManager)
    {
        _logger = logger;
        _rewardManager = rewardManager;
        _questManager = questManager;
        _resourceManager = resourceManager;
    }

    #region Scripts

    public async Task<string?> SetScriptAsync(Npc npc, string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            npc.GmScript = null;
            UpdateInteraction(npc);
            return null;
        }

        var (sandbox, error) = await ScriptSandbox.CompileAsync(npc, source, $"npc {npc.Guid}");

        if (sandbox is null)
            return error;

        npc.GmScript = sandbox;

        lock (npc.NearbyPlayers)
            npc.NearbyPlayers.Clear();

        UpdateInteraction(npc);

        var spawnError = await sandbox.CallAsync("onSpawn");

        return spawnError is null ? null : $"Saved, but onSpawn failed: {spawnError}";
    }

    public async Task<string?> TestScriptAsync(Npc npc, Player player)
    {
        if (npc.GmScript is not { } sandbox)
            return "This has no script.";

        return await sandbox.CallAsync("onSpawn")
            ?? await sandbox.CallAsync("onInteract", new ScriptPlayer(player, _rewardManager, _questManager, _resourceManager));
    }

    private void FireHook(Npc npc, string hook, Player? player = null, string? argument = null)
    {
        if (npc.GmScript is not { } sandbox || !sandbox.HasHook(hook))
            return;

        _ = RunHookAsync(npc, sandbox, hook, player, argument);
    }

    public void OnMobHurt(Npc npc, Player attacker, int damage)
    {
        if (npc.GmScript is not { } sandbox || !sandbox.HasHook("onHurt"))
            return;

        _ = RunHurtHookAsync(npc, sandbox, attacker, damage);
    }

    public void OnMobDied(Npc npc, Player killer)
    {
        FireHook(npc, "onDeath", killer);

        var zoneScripts = npc.Zone.GetOrCreateScriptContext();

        if (zoneScripts.HasCallbacks("mobKilled"))
            zoneScripts.FireEvent("mobKilled", npc, WrapPlayer(killer));
    }

    public void OnMobRespawned(Npc npc) => FireHook(npc, "onRespawn");

    private async Task RunHurtHookAsync(Npc npc, ScriptSandbox sandbox, Player attacker, int damage)
    {
        var error = await sandbox.CallAsync("onHurt", WrapPlayer(attacker), damage);

        if (error is not null)
            _logger.LogWarning("Script on NPC {NpcGuid} failed in {Hook}: {Error}", npc.Guid, "onHurt", error);
    }

    private async Task RunHookAsync(Npc npc, ScriptSandbox sandbox, string hook, Player? player, string? argument)
    {
        var scriptPlayer = player is null ? null : new ScriptPlayer(player, _rewardManager, _questManager, _resourceManager);

        var error = await sandbox.CallAsync(hook, scriptPlayer, argument);

        if (error is not null)
            _logger.LogWarning("Script on NPC {NpcGuid} failed in {Hook}: {Error}", npc.Guid, hook, error);
    }

    private async Task RunTickAsync(Npc npc, ScriptSandbox sandbox)
    {
        var error = await sandbox.TickAsync();

        if (error is not null)
            _logger.LogWarning("Script on NPC {NpcGuid} failed in onSecond: {Error}", npc.Guid, error);
    }

    public IScriptablePlayer WrapPlayer(Player player) => new ScriptPlayer(player, _rewardManager, _questManager, _resourceManager);

    #endregion

    #region Signals

    private const int MaxSignalsPerSecond = 10;
    private const int MaxSignalNameLength = 64;
    private const int MaxSignalDataLength = 256;

    public bool Signal(Npc from, string name, string? data, float radius)
    {
        if (name.Length is 0 or > MaxSignalNameLength || data?.Length > MaxSignalDataLength)
            return false;

        if (++from.ScriptSignalsThisSecond > MaxSignalsPerSecond)
            return false;

        foreach (var npc in from.Zone.Npcs)
        {
            if (npc == from || npc.GmScript is not { } sandbox || !sandbox.HasHook("onSignal"))
                continue;

            if (radius > 0 && !npc.Position.IsInRange(from.Position, radius))
                continue;

            _ = RunSignalAsync(npc, sandbox, name, data, from.Guid);
        }

        return true;
    }

    private async Task RunSignalAsync(Npc npc, ScriptSandbox sandbox, string name, string? data, ulong fromGuid)
    {
        var error = await sandbox.CallSignalAsync(name, data, fromGuid);

        if (error is not null)
            _logger.LogWarning("Script on NPC {NpcGuid} failed in onSignal: {Error}", npc.Guid, error);
    }

    #endregion

    #region Interaction

    public void SetConversation(Npc npc, Conversation? conversation)
    {
        npc.Conversation = conversation;
        UpdateInteraction(npc);
    }

    /// <summary>
    /// Makes the NPC clickable while it has an onInteract hook or a conversation. Props also get the
    /// cursor they otherwise lack, so players can click them.
    /// </summary>
    private void UpdateInteraction(Npc npc)
    {
        var interactive = npc.GmScript?.HasHook("onInteract") == true || npc.Conversation is not null;

        npc.InteractAction = interactive ? player => OnInteract(npc, player) : null;

        if (npc.IsWorldProp)
        {
            npc.IsInteractable = interactive;
            npc.HasCursor = interactive ? true : null;
            npc.CursorId = interactive ? InteractionMenuHelper.RefereeCursorId : (byte)0;
        }

        npc.RefreshForVisiblePlayers();
    }

    public void OnInteract(Npc npc, Player player)
    {
        if (!InteractionMenuHelper.CanInteract(npc, player))
            return;

        // A script that handles clicks decides for itself whether to start the conversation.
        if (npc.GmScript?.HasHook("onInteract") == true)
        {
            FireHook(npc, "onInteract", player);
            return;
        }

        StartConversation(player, npc);
    }

    public bool OnHear(Npc npc, Player player, string message)
    {
        if (npc.GmScript?.HasHook("onHear") != true)
            return false;

        FireHook(npc, "onHear", player, message);
        return true;
    }

    public void OnSecond(Npc npc)
    {
        npc.ScriptSignalsThisSecond = 0;

        if (npc.GmScript is { NeedsTick: true } sandbox)
            _ = RunTickAsync(npc, sandbox);

        if (npc.ScriptArrivalPending && !npc.IsMoving)
        {
            npc.ScriptArrivalPending = false;
            FireHook(npc, "onArrive");
        }

        var tracksProximity = npc.GmScript is { } script && (script.HasHook("onProximity") || script.HasHook("onLeave"));

        foreach (var player in npc.VisiblePlayers.Values)
        {
            var distance = Distance(npc, player);

            if (tracksProximity)
            {
                bool entered;
                bool left;

                lock (npc.NearbyPlayers)
                {
                    entered = distance <= ProximityRange && npc.NearbyPlayers.Add(player.Guid);
                    left = distance > ProximityExitRange && npc.NearbyPlayers.Remove(player.Guid);
                }

                if (entered)
                    FireHook(npc, "onProximity", player);
                else if (left)
                    FireHook(npc, "onLeave", player);
            }

            if (player.ActiveConversation?.NpcGuid == npc.Guid && distance > ConversationRange)
                EndConversation(player);
        }

        // Forget players who left view, so they trigger again when they come back.
        lock (npc.NearbyPlayers)
            npc.NearbyPlayers.RemoveWhere(guid => !npc.VisiblePlayers.ContainsKey(guid));
    }

    #endregion

    #region Conversations

    public bool StartConversation(Player player, Npc npc, int? nodeId = null)
    {
        var node = npc.Conversation?.GetNode(nodeId ?? npc.Conversation.Start);

        if (node is null)
            return false;

        player.ActiveConversation = (npc.Guid, node.Id);

        // The client's own NPC dialog gives the camera zoom onto the NPC. Its pane only shows locale strings,
        // so it is sent empty, which the dialog mod (ClientMods/dialog) hides; the panel draws the text in its place.
        player.SendTunneled(new CommandPacketShowDialog
        {
            NpcGuid = npc.Guid,
            CameraFocusParam = 1f
        });

        // dialog: npc guid, npc name, node id, text, then "<index>:<choice text>" per choice.
        ChatHelper.SendGmPanelData(player, "dialog",
        [
            npc.Guid,
            PanelTextHelper.Escape(npc.Name ?? string.Empty),
            node.Id,
            PanelTextHelper.Escape(node.Text),
            .. node.Choices.Select((choice, index) => $"{index}:{PanelTextHelper.Escape(choice.Text)}")
        ]);

        // Lets everyone nearby see what the NPC says.
        npc.Say(node.Text);

        return true;
    }

    public void Choose(Player player, ulong npcGuid, int nodeId, int index)
    {
        if (player.ActiveConversation is not { } active || active.NpcGuid != npcGuid || active.NodeId != nodeId)
            return;

        if (!player.Zone.TryGetNpc(npcGuid, out var npc) || Distance(npc, player) > ConversationRange)
        {
            EndConversation(player);
            return;
        }

        var choice = npc.Conversation?.GetNode(nodeId)?.Choices.ElementAtOrDefault(index);

        if (choice is null)
            return;

        if (choice.GiveItemId != 0)
            _rewardManager.TryGrantItem(player, choice.GiveItemId, 0, choice.GiveQuantity, npc.Guid);

        if (!string.IsNullOrEmpty(choice.Hook))
            FireHook(npc, "onChoice", player, choice.Hook);

        if (choice.Next == 0 || !StartConversation(player, npc, choice.Next))
            EndConversation(player);
    }

    public void EndConversation(Player player)
    {
        if (player.ActiveConversation is null)
            return;

        player.ActiveConversation = null;

        player.SendTunneled(new CommandPacketEndDialog());
        ChatHelper.SendGmPanelData(player, "dialog", "close");
    }

    #endregion

    private static float Distance(Npc npc, Player player) => Vector3.Distance(
        new Vector3(npc.Position.X, npc.Position.Y, npc.Position.Z),
        new Vector3(player.Position.X, player.Position.Y, player.Position.Z));
}
