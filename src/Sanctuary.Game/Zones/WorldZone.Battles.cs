using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Battles;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Zones;

/// <summary>
/// Battles: a battle is fought in its own copy of a world zone (see <see cref="BattleManager"/>), which spawns
/// its enemies and its Exit.
/// </summary>
public sealed partial class WorldZone
{
    // The cursor the live servers gave a battle's enemies (the wolves of Frostfang Growler!).
    private const byte EnemyCursorId = 11;

    /// <summary>
    /// The battle this copy of the zone is for; null for a zone everyone shares.
    /// </summary>
    public Battle? Battle { get; internal set; }

    /// <summary>
    /// A battle's copy stays up while its battle runs, even with nobody in it (the start panel is up, or everyone's
    /// between respawns and teleports); the battle closes it. So does a 3D game's (see <see cref="Arena"/>).
    /// </summary>
    public override bool IsEmpty => base.IsEmpty && Battle is null && Arena is null;

    /// <summary>
    /// An enemy that fights as <paramref name="mobKey"/> (Mobs.json) and stays down once beaten.
    /// </summary>
    public bool TrySpawnBattleEnemy(NpcDefinition definition, string? mobKey, Vector4 position, float heading, [MaybeNullWhen(false)] out Npc enemy)
    {
        if (!TryCreateNpc(null, definition, out enemy))
            return false;

        // Hostile and clickable before it's placed: players see it as it was when it came into view. Live gave each
        // enemy the attack cursor, whatever it was out in the world.
        enemy.Disposition = Npc.HostileDisposition;
        enemy.HasCursor = true;
        enemy.CursorId = EnemyCursorId;
        enemy.RelevanceUnknown2 = true;
        enemy.MobKey = mobKey;

        // Fought, not talked to: anything its definition does when clicked out in the world would make it clickable
        // instead of a target.
        enemy.InteractAction = null;
        enemy.InteractionList = null;
        enemy.OpensAtlas = false;

        SyncCombat(enemy);

        if (enemy.Combat is null)
        {
            enemy.Dispose();
            enemy = null;
            return false;
        }

        enemy.Combat.Respawns = false;

        Place(enemy, position, heading);

        return true;
    }

    /// <summary>
    /// Something that just stands there and does <paramref name="onInteract"/> when clicked (the Exit, a pick-up).
    /// </summary>
    public bool TrySpawnBattleProp(NpcDefinition definition, Vector4 position, float heading, Action<Player> onInteract, [MaybeNullWhen(false)] out Npc prop)
    {
        if (!TryCreateNpc(null, definition, out prop))
            return false;

        prop.Disposition = 1;

        // Clickable before it's placed, so players see the prompt as it comes into view. The click goes to it rather
        // than the definition's own list (the live Exit's interaction 4490, which nothing here answers).
        prop.InteractionList = null;
        prop.InteractAction = onInteract;

        Place(prop, position, heading);

        return true;
    }

    private static void Place(Npc npc, Vector4 position, float heading)
    {
        var rotation = QuaternionExtensions.FromHeading(heading);

        npc.SpawnPosition = position;
        npc.SpawnRotation = rotation;
        npc.UpdatePosition(position, rotation);
    }

    /// <summary>
    /// A player was knocked out here.
    /// </summary>
    public void OnPlayerKnockedOut(Player player) => _battleManager.OnKnockedOut(player);
}
