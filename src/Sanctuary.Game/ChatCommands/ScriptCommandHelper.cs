using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Sanctuary.Game.Authoring;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;
using Sanctuary.Scripting;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// "!npc script ..." and "!prop script ...": edits the GM script on the selected world NPC or prop.
/// Source arrives from the GM panel in escaped chunks (see <see cref="PanelTextHelper"/>) and is
/// compiled on commit; results go back as script|ok and script|error panel lines.
/// </summary>
public static class ScriptCommandHelper
{
    public const string Usage = "script show | append <escaped chunk> | commit | cancel | clear | test | use <library name> [key=value ...]";

    /// <summary>
    /// Panel lines carry at most this much escaped source each.
    /// </summary>
    private const int ChunkLength = 200;

    /// <param name="rawArgs">The text after "script", exactly as typed.</param>
    public static bool Handle(IChatCommand command, IChatCommandManager chatCommandManager, IScriptLibrary scriptLibrary, Player invoker, Npc target, string kind, string[] args, string rawArgs)
    {
        if (invoker.ChatCommandRole < ChatCommandRole.Admin)
        {
            ChatHelper.SendSystemMessage(invoker, "Only admins can edit scripts.");
            return true;
        }

        if (args.Length == 0)
            return false;

        if (target.Zone is not WorldZone worldZone || !(worldZone.IsWorldNpc(target.Guid) || worldZone.IsWorldFixture(target.Guid)))
        {
            ChatHelper.SendSystemMessage(invoker, "Only NPCs and props saved in a world zone can have scripts.");
            return true;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "show":
                SendSource(invoker, target, kind, worldZone.GetSavedScript(target.Guid) ?? string.Empty);
                return true;

            case "append":
                if (args.Length < 2)
                    return false;

                // Raw, so indentation and runs of spaces in the source survive.
                var chunk = ChatCommandText.Remainder(rawArgs, 1);

                invoker.PendingScript ??= new StringBuilder();

                if (invoker.PendingScript.Length + chunk.Length > ScriptSandbox.MaxSourceLength * 2)
                {
                    invoker.PendingScript = null;
                    SendResult(invoker, false, $"Script too long; at most {ScriptSandbox.MaxSourceLength} characters.");
                    return true;
                }

                invoker.PendingScript.Append(chunk);
                return true;

            case "cancel":
                invoker.PendingScript = null;
                return true;

            case "commit":
                if (invoker.PendingScript is null)
                {
                    SendResult(invoker, false, "Nothing to commit; append the script first.");
                    return true;
                }

                var source = PanelTextHelper.Unescape(invoker.PendingScript.ToString());
                invoker.PendingScript = null;

                if (!TryFillDefaults(source, out var filled, out var fillError))
                {
                    SendResult(invoker, false, fillError!);
                    return true;
                }

                _ = CommitAsync(command, chatCommandManager, invoker, worldZone, target, filled);
                return true;

            case "clear":
                _ = CommitAsync(command, chatCommandManager, invoker, worldZone, target, null);
                return true;

            case "test":
                _ = TestAsync(invoker, target);
                return true;

            case "use":
                if (args.Length < 2)
                    return false;

                if (!TryRenderLibraryScript(scriptLibrary, args[1], args[2..], out var rendered, out var renderError))
                {
                    SendResult(invoker, false, renderError!);
                    return true;
                }

                _ = CommitAsync(command, chatCommandManager, invoker, worldZone, target, rendered);
                return true;

            default:
                return false;
        }
    }

    private static async Task CommitAsync(IChatCommand command, IChatCommandManager chatCommandManager, Player invoker, WorldZone worldZone, Npc target, string? source)
    {
        var (saved, error) = await ApplyAsync(worldZone, target, source);

        if (saved)
        {
            chatCommandManager.LogAction(command, invoker, source is null ? "ScriptClear" : "ScriptSave", null,
                $"Guid: {target.Guid}, Length: {source?.Length ?? 0}");
        }

        if (error is not null)
            SendResult(invoker, false, error);
        else
            SendResult(invoker, true, source is null ? "Script removed." : "Script saved and running.");
    }

    /// <summary>
    /// Runs a world NPC's or prop's new script (null removes it) and saves it if it compiled: a compile error
    /// leaves the old script running and saves nothing, while an onSpawn error still saves. Also used by the admin API.
    /// </summary>
    public static async Task<(bool Saved, string? Error)> ApplyAsync(WorldZone worldZone, Npc target, string? source)
    {
        var error = await target.Zone.NpcContent.SetScriptAsync(target, source);
        var compiled = source is null ? target.GmScript is null : target.GmScript?.Source == source;

        if (compiled)
            worldZone.TrySaveScript(target, source);

        return (compiled, error);
    }

    /// <summary>
    /// A library template loaded into an editor still has its {{placeholders}}; this fills them with the header defaults.
    /// </summary>
    public static bool TryFillDefaults(string source, out string filled, out string? error)
        => ScriptTemplate.Parse(source, null, null, false).TryRender(new Dictionary<string, string>(), out filled, out error);

    /// <summary>
    /// Whether a library script compiles with its parameter defaults filled in, checked on an unregistered stand-in
    /// NPC so nothing in the zone is touched. Null when it does, else why not.
    /// </summary>
    public static async Task<string?> CheckLibraryScriptAsync(IZone zone, string name, string source, string? description)
    {
        var entry = ScriptTemplate.Parse(source, name, description, false);

        if (!entry.TryRender(new Dictionary<string, string>(), out var rendered, out var renderError))
            return $"Parameter defaults don't fit their types: {renderError}";

        var (_, compileError) = await ScriptSandbox.CompileAsync(new Npc(zone), rendered, $"lib {name}");

        return compileError;
    }

    private static async Task TestAsync(Player invoker, Npc target)
    {
        var error = await target.Zone.NpcContent.TestScriptAsync(target, invoker);

        SendResult(invoker, error is null, error ?? "Ran onSpawn and onInteract without errors.");
    }

    /// <summary>
    /// Renders a library script with "key=value" arguments, whose values are escaped (see <see cref="PanelTextHelper"/>).
    /// </summary>
    private static bool TryRenderLibraryScript(IScriptLibrary scriptLibrary, string name, string[] arguments, out string source, out string? error)
    {
        source = string.Empty;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var argument in arguments)
        {
            var separator = argument.IndexOf('=');

            if (separator <= 0)
            {
                error = $"Expected key=value, got '{argument}'.";
                return false;
            }

            values[argument[..separator]] = PanelTextHelper.Unescape(argument[(separator + 1)..]);
        }

        return TryRenderLibraryScript(scriptLibrary, name, values, out source, out error);
    }

    /// <summary>
    /// A library script with its parameters filled from values (the rest from their defaults).
    /// </summary>
    public static bool TryRenderLibraryScript(IScriptLibrary scriptLibrary, string name, IReadOnlyDictionary<string, string> values, out string source, out string? error)
    {
        source = string.Empty;

        if (!scriptLibrary.TryGet(name, out var entry))
        {
            error = $"No library script named {name}. Use !scriptlib list.";
            return false;
        }

        return entry.TryRender(values, out source, out error);
    }

    private static void SendSource(Player invoker, Npc target, string kind, string source)
        => SendSource(invoker, kind, target.Guid, source);

    /// <summary>
    /// Sends source to the panel's script editor as script|begin, script|chunk... and script|end.
    /// </summary>
    public static void SendSource(Player invoker, string kind, object id, string source)
    {
        var escaped = PanelTextHelper.Escape(source);

        ChatHelper.SendGmPanelData(invoker, "script", "begin", kind, id);

        for (var i = 0; i < escaped.Length; i += ChunkLength)
            ChatHelper.SendGmPanelData(invoker, "script", "chunk", escaped.Substring(i, Math.Min(ChunkLength, escaped.Length - i)));

        ChatHelper.SendGmPanelData(invoker, "script", "end");
    }

    public static void SendResult(Player invoker, bool ok, string message)
    {
        ChatHelper.SendGmPanelData(invoker, "script", ok ? "ok" : "error", PanelTextHelper.Escape(message));
        ChatHelper.SendSystemMessage(invoker, ok ? message : $"Script error: {message}");
    }
}
