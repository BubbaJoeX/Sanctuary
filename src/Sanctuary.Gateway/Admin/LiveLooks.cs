using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Sanctuary.Game;
using Sanctuary.Game.Entities;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Admin;

/// <summary>
/// One actor of a look, as the client puts a character together: its actor file (.adr, or an .agr actor set that
/// lists more parts), the texture aliases it wears (one per material slot, e.g. skintone3 and a face paint), its
/// tint alias (a named colour on the actor) and a Tints.xml id (a dye or hair colour; 0 for none), and the
/// equipment slot it's worn in.
/// </summary>
public sealed record LookPart(string File, IReadOnlyList<string> Textures, string? Tint, int TintId, string? Slot);

/// <summary>
/// How the zone editor draws the players and NPCs of the live map: the same parts the client gets in its AddPc and
/// AddNpc packets. Looks are sent once each and referred to by key, since many NPCs share one.
/// </summary>
public static class LiveLooks
{
    private const int HeadSlot = 1;
    private const int WholeBodyCostumeSlot = 17;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// A player: their race's body in their skin tone, head with face paint, hair and facial hair in their hair
    /// colour, and what they wear. Item and customization actors name the race and gender as &lt;race&gt; and
    /// &lt;gender&gt;, and hair as &lt;hairtype&gt;: "hat" under headgear, "full" without. A whole-body costume is
    /// all there is to see.
    /// </summary>
    public static IReadOnlyList<LookPart> Of(Player player, IResourceManager resourceManager)
    {
        if (!resourceManager.Models.TryGetValue(player.Model, out var body))
            return [];

        var attachments = player.GetAttachments();

        if (attachments.FirstOrDefault(x => x.Slot == WholeBodyCostumeSlot) is { } costume)
            return [Part(costume, resourceManager, name => name)];

        // human_m.adr, fairy_f.adr: the race and gender the templates take.
        var stem = System.IO.Path.GetFileNameWithoutExtension(body.ModelFileName);
        var split = stem.LastIndexOf('_');
        var race = split > 0 ? stem[..split] : stem;
        var gender = split > 0 ? stem[(split + 1)..] : "m";
        var hairType = attachments.Any(x => x.Slot == HeadSlot) ? "hat" : "full";

        string Resolve(string name) => name
            .Replace("<race>", race, StringComparison.OrdinalIgnoreCase)
            .Replace("<gender>", gender, StringComparison.OrdinalIgnoreCase)
            .Replace("<hairtype>", hairType, StringComparison.OrdinalIgnoreCase);

        var parts = new List<LookPart>
        {
            new(body.ModelFileName, [player.SkinTone], null, 0, null),
            new(Resolve(player.Head), [player.SkinTone, string.IsNullOrEmpty(player.FacePaint) ? "none" : player.FacePaint], null, 0, "CustomizationHead"),
            new(Resolve(player.Hair), [], null, player.HairColor, "CustomizationHair")
        };

        // A human's facial hair takes the hair colour; the same slot holds a pixie's wings, which keep their own.
        if (!string.IsNullOrEmpty(player.ModelCustomization))
        {
            var customization = Resolve(player.ModelCustomization);
            var hairColour = customization.Contains("hair", StringComparison.OrdinalIgnoreCase) ? player.HairColor : 0;

            parts.Add(new LookPart(customization, [], null, hairColour, "CustomizationModel"));
        }

        parts.AddRange(attachments.Where(x => !string.IsNullOrEmpty(x.ModelName)).Select(x => Part(x, resourceManager, Resolve)));

        return parts;
    }

    /// <summary>
    /// An NPC, mob or placed prop: its model with the texture and tint aliases and tint it spawns with, and any gear.
    /// </summary>
    public static IReadOnlyList<LookPart> Of(Npc npc, IResourceManager resourceManager)
    {
        if (!resourceManager.Models.TryGetValue(npc.ModelId, out var model))
            return [];

        var parts = new List<LookPart>
        {
            new(model.ModelFileName, string.IsNullOrEmpty(npc.TextureAlias) ? [] : [npc.TextureAlias], NullIfEmpty(npc.TintAlias), npc.TintId, null)
        };

        parts.AddRange(npc.Attachments.Where(x => !string.IsNullOrEmpty(x.ModelName)).Select(x => Part(x, resourceManager, name => name)));

        return parts;
    }

    /// <summary>
    /// A look's key (a hash of it) and its JSON, which goes to the editor once per feed.
    /// </summary>
    public static (string Key, string Json) Key(IReadOnlyList<LookPart> look)
    {
        var json = JsonSerializer.Serialize(look, JsonOptions);

        return (Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(json)))[..12], json);
    }

    private static LookPart Part(CharacterAttachmentData attachment, IResourceManager resourceManager, Func<string, string> resolve) => new(
        resolve(attachment.ModelName),
        string.IsNullOrEmpty(attachment.TextureAlias) ? [] : [attachment.TextureAlias],
        NullIfEmpty(attachment.TintAlias),
        attachment.TintId,
        resourceManager.EquipmentSlots.TryGetValue(attachment.Slot, out var slot) ? slot.SlotName : null);

    private static string? NullIfEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;
}
