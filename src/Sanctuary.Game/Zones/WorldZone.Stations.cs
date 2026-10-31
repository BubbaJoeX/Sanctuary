using System;
using System.Collections.Generic;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Interactions;
using Sanctuary.Game.Minigames;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Zones;

/// <summary>
/// Stations (Stations.json): a cooking table, anvil or other crafting station opens the recipe window on its
/// recipes, and a game table, harvest field, mining vein or postbox starts its minigame. With more than one kind of
/// recipe or game, clicking asks which.
/// </summary>
public sealed partial class WorldZone
{
    // The recipe window's filters, by the client's own words for them.
    private static readonly Dictionary<int, int> RecipeFilterTextIds = new()
    {
        [9] = 15293, // Smelting
        [10] = 20226, // Cooking
        [12] = 19201 // Forging
    };

    private void SetUpStation(Npc npc, NpcDefinition definition)
    {
        if (npc.InteractAction is not null || !_resourceManager.Stations.ContainsKey(definition.Id))
            return;

        // Most stations (cooking tables, game tables) are props the client won't let players click without a cursor.
        if (npc.HasCursor != true)
        {
            npc.IsInteractable = true;
            npc.HasCursor = true;
            npc.CursorId = InteractionMenuHelper.RefereeCursorId;
        }

        // The station is looked up again on the click, so a reload can change it.
        npc.InteractAction = player =>
        {
            if (!InteractionMenuHelper.CanInteract(npc, player) || !_resourceManager.Stations.TryGetValue(definition.Id, out var station))
                return;

            if (player.IsInCombat || player.IsKnockedOut)
            {
                ChatHelper.SendSystemMessage(player, "Finish what you're doing first!");
                return;
            }

            var options = GetStationOptions(station);

            if (options.Count == 1)
                options[0].Invoke(player);
            else if (options.Count > 1)
                InteractionMenuHelper.TryOpenMenu(player, npc, options);
            else
                ChatHelper.SendSystemMessage(player, "There's nothing to do here yet.");
        };
    }

    private List<NpcInteractionOption> GetStationOptions(StationDefinition station)
    {
        var options = new List<NpcInteractionOption>();

        if (_craftingManager is { } craftingManager)
        {
            foreach (var filter in station.RecipeFilters)
            {
                options.Add(new NpcInteractionOption
                {
                    IconId = ContextIcons.QuestOffer,
                    ButtonTextId = RecipeFilterTextIds.GetValueOrDefault(filter, 20226),
                    Invoke = player => craftingManager.OpenStation(player, filter)
                });
            }
        }

        // Levels of one game (the same name) are one choice, picked at random; different games are a choice each.
        var games = station.MinigameIds
            .Where(id => _resourceManager.Minigames.ContainsKey(id))
            .Select(id => _resourceManager.Minigames[id])
            .GroupBy(game => game.NameId);

        foreach (var game in games)
        {
            var ids = game.Select(definition => definition.Id).ToArray();

            options.Add(new NpcInteractionOption
            {
                IconId = ContextIcons.QuestOffer,
                ButtonTextId = game.Key,
                Invoke = player => MinigameManager.Join(player, ids[Random.Shared.Next(ids.Length)])
            });
        }

        return options;
    }
}
