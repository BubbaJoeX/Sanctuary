using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game;

public interface IZoneManager
{
    int StartingZoneDefinitionId { get; }

    WorldZone StartingZone { get; }

    IEnumerable<IZone> Zones { get; }

    bool Load();

    bool TryGetPlayer(ulong guid, [MaybeNullWhen(false)] out Player player);
    bool TryGetPlayer(string name, [MaybeNullWhen(false)] out Player player);

    bool TryMovePlayerToZone(int zoneDefinitionId, ulong? ownerId, Player player, Vector4 position, Quaternion rotation, out IZone zone);

    /// <summary>
    /// The zone's instance for <paramref name="ownerId"/>, started if it isn't running yet.
    /// </summary>
    bool TryGetOrCreateZoneInstance(int zoneDefinitionId, ulong? ownerId, [MaybeNullWhen(false)] out IZone zone);

    bool TryRemoveZoneInstance(IZone zone);
    void EvictIfEmpty(IZone zone);
}