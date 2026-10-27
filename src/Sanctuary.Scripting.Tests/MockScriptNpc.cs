using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Scripting.Tests;

internal class MockScriptNpc(IScriptableZone _zone) : IScriptableNpc
{
    public ulong Guid { get => 0; init => throw new NotImplementedException(); }
    public string? Name { get => "MockNpc"; set => throw new NotImplementedException(); }
    public ILogger Logger { get; } = _zone.Logger;
    public ScriptRuntime ScriptRuntime => _zone.ScriptRuntime;
    public IScriptableZone Zone => _zone;
    public (float x, float y, float z) Position => (0, 0, 0);

    public ScriptContext GetOrCreateScriptContext()
    {
        throw new NotImplementedException();
    }

    public void Say(string message)
    {
        throw new NotImplementedException();
    }

    public void SayLocalized(int stringId)
    {
        throw new NotImplementedException();
    }

    public void MoveTo(float x, float y, float z, bool direct)
    {
        throw new NotImplementedException();
    }

    public List<(int AnimationId, bool Loop)> Emotes { get; } = [];

    public void Emote(int animationId, bool loop) => Emotes.Add((animationId, loop));

    public void Face(IScriptablePlayer player)
    {
        throw new NotImplementedException();
    }

    public IReadOnlyList<ulong> GetNearbyPlayerGuids(float radius)
    {
        throw new NotImplementedException();
    }

    public void StartConversation(IScriptablePlayer player, int? nodeId)
    {
        throw new NotImplementedException();
    }

    public void PlayAnimation(int animationId)
    {
        throw new NotImplementedException();
    }

    public void SetBaseAnimation(int animationId)
    {
        throw new NotImplementedException();
    }

    public IReadOnlyList<ulong> GetPlayerGuidsOnTop(float horizontalRadius, float minHeight, float maxHeight)
    {
        throw new NotImplementedException();
    }

    public void SetCollidable(bool collidable)
    {
        throw new NotImplementedException();
    }

    public int AddEffectTag(int compositeEffectId)
    {
        throw new NotImplementedException();
    }

    public void RemoveEffectTag(int tagId)
    {
        throw new NotImplementedException();
    }

    public bool IsMoving => false;

    public void SetSpeed(float speed)
    {
        throw new NotImplementedException();
    }

    public void SetHeading(float heading)
    {
        throw new NotImplementedException();
    }

    public void PlayEffect(int compositeEffectId)
    {
        throw new NotImplementedException();
    }

    public void Remove(int compositeEffectId)
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

    public (float x, float y, float z) HomePosition => (5, 0, 5);

    public List<IScriptablePlayer> Players { get; } = [];

    public IReadOnlyList<IScriptablePlayer> GetNearbyPlayers(float radius) => Players;

    public IScriptablePlayer? GetPlayer(ulong guid) => Players.Find(player => player.Guid == guid);

    public List<(ulong Guid, string Message)> Whispers { get; } = [];

    public void SayTo(IScriptablePlayer player, string message) => Whispers.Add((player.Guid, message));

    public float Scale { get; private set; } = 1;

    public void SetScale(float scale) => Scale = scale;

    public void Rename(string name)
    {
        throw new NotImplementedException();
    }

    public List<(string Name, string? Data, float Radius)> Signals { get; } = [];

    public bool Signal(string name, string? data, float radius)
    {
        Signals.Add((name, data, radius));
        return true;
    }

    public bool IsMob { get; set; }

    public int Health { get; set; }

    public int MaxHealth { get; set; }

    public bool IsDead => IsMob && Health <= 0;

    public ulong TargetGuid { get; set; }

    public int Hurt(IScriptablePlayer attacker, int damage)
    {
        var taken = Math.Min(damage, Health);
        Health -= taken;
        return taken;
    }

    public List<(ulong Guid, int Damage)> Attacks { get; } = [];

    public int Attack(IScriptablePlayer player, int damage)
    {
        Attacks.Add((player.Guid, damage));
        return damage;
    }
}
