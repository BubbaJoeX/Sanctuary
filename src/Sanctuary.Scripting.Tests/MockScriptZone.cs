using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Scripting.Tests;

internal class MockScriptZone(ILogger _logger) : IScriptableZone
{
    public IScriptManager ScriptManager => throw new NotImplementedException();

    public int Id { get; set; }

    public string Name => "MockZone";

    public ILogger Logger { get; } = _logger;

    public ScriptRuntime ScriptRuntime { get; } = new ScriptRuntime(_logger);

    public ScriptContext GetOrCreateScriptContext()
    {
        throw new NotImplementedException();
    }

    public bool TryAddScript(string scriptName)
    {
        throw new NotImplementedException();
    }

    public bool TryRemoveScript(string scriptName)
    {
        throw new NotImplementedException();
    }

    public void SetClientAreaEnabled(int areaId, bool enabled)
    {
        throw new NotImplementedException();
    }

    public List<string> Broadcasts { get; } = [];

    public void Broadcast(string message) => Broadcasts.Add(message);

    public List<(string Message, float X, float Z, float Radius)> NearBroadcasts { get; } = [];

    public void BroadcastNear(string message, float x, float z, float radius) => NearBroadcasts.Add((message, x, z, radius));

    public bool IsSeasonActive(string seasonName) => ActiveEvents.Contains(seasonName);

    public bool TrySpawnNpc(int npcId, ulong? npcGuid, float x, float y, float z, float heading, float pitch, float roll, [MaybeNullWhen(false)] out IScriptableNpc npc)
    {
        throw new NotImplementedException();
    }

    public List<IScriptablePlayer> Players { get; } = [];

    public int PlayerCount => Players.Count;

    public IReadOnlyList<IScriptablePlayer> GetPlayers() => Players;

    public IScriptablePlayer? GetPlayer(ulong guid, string? name) =>
        Players.Find(player => guid != 0 ? player.Guid == guid : player.Name == name);

    public HashSet<string> ActiveEvents { get; } = [];

    public bool IsEventActive(string eventKey) => ActiveEvents.Contains(eventKey);

    public float GameHour { get; set; } = 18.5f;

    public int BattleId { get; set; }

    public ulong HouseOwnerGuid { get; set; }
}
