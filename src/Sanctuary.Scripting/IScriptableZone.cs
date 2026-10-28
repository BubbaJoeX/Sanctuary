using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Sanctuary.Scripting;

public interface IScriptableZone : IScriptable
{
    IScriptManager ScriptManager { get; }

    int Id { get; }
    string Name { get; }

    void SetClientAreaEnabled(int areaId, bool enabled);

    void Broadcast(string message);

    void BroadcastNear(string message, float x, float z, float radius);

    bool IsSeasonActive(string seasonName);

    /// <summary>
    /// A staff event (!event on) is on.
    /// </summary>
    bool IsEventActive(string eventKey);

    bool TrySpawnNpc(int npcId, ulong? npcGuid, float x, float y, float z, float heading, float pitch, float roll, [MaybeNullWhen(false)] out IScriptableNpc npc);

    int PlayerCount { get; }

    IReadOnlyList<IScriptablePlayer> GetPlayers();

    /// <summary>
    /// A player in this zone, by guid or else by name, or null.
    /// </summary>
    IScriptablePlayer? GetPlayer(ulong guid, string? name);
}
