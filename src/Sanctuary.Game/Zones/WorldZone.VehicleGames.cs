using System.Diagnostics.CodeAnalysis;
using System.Numerics;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.VehicleGames;

namespace Sanctuary.Game.Zones;

/// <summary>
/// The 3D games: a kart race, demolition derby or soccer match (or a garage visit) is played in its own copy of the
/// game's zone (see <see cref="VehicleGameManager"/>), which spawns its drivers and players.
/// </summary>
public sealed partial class WorldZone
{
    /// <summary>
    /// The game this copy of the zone is for; null for a zone everyone shares.
    /// </summary>
    public ArenaMatch? Arena { get; internal set; }

    /// <summary>
    /// A game's driver or player: an NPC that only stands where the game puts it, shown with the game's character
    /// state bit.
    /// </summary>
    public bool TrySpawnArenaNpc(NpcDefinition definition, Vector4 position, float heading, int characterState, [MaybeNullWhen(false)] out Npc npc)
    {
        if (!TryCreateNpc(null, definition, out npc))
            return false;

        npc.Disposition = 1;
        npc.CharacterState = characterState;

        var rotation = QuaternionExtensions.FromHeading(heading);

        npc.SpawnPosition = position;
        npc.SpawnRotation = rotation;
        npc.UpdatePosition(position, rotation);

        return true;
    }
}
