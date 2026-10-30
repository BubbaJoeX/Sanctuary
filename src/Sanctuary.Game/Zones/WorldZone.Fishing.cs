using Sanctuary.Game.Entities;
using Sanctuary.Game.Fishing;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Zones;

/// <summary>
/// Fishing: a world zone that's one of Fishing.json's ponds runs a <see cref="FishingPond"/>, and Fabled Realms'
/// fishing holes (the docks by its ponds and lakes) send players to theirs.
/// </summary>
public sealed partial class WorldZone
{
    /// <summary>
    /// The zone's fishing, or null if it isn't a pond.
    /// </summary>
    public FishingPond? Fishing { get; }

    protected override void UpdateEveryTick()
    {
        base.UpdateEveryTick();

        Fishing?.Update();
    }

    protected override void OnPlayerRemoved(Player player)
    {
        base.OnPlayerRemoved(player);

        Fishing?.OnPlayerLeft(player);
    }

    protected override void OnNpcCreated(Npc npc, NpcDefinition definition)
    {
        base.OnNpcCreated(npc, definition);

        if (!_resourceManager.Fishing.TryGetPondByHole(definition.Id, out _))
            return;

        // Clicking the dock goes fishing. The pond is looked up again on the click, so a reload can move it.
        npc.InteractAction = player =>
        {
            if (InteractionMenuHelper.CanInteract(npc, player) && _resourceManager.Fishing.TryGetPondByHole(definition.Id, out var pond))
                FishingPond.GoFishing(player, pond);
        };
    }
}
