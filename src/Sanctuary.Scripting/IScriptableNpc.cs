using System.Collections.Generic;
using System.Numerics;

namespace Sanctuary.Scripting;

public interface IScriptableNpc : IScriptable
{
    ulong Guid { get; }
    string? Name { get; set; }
    IScriptableZone Zone { get; }
    (float x, float y, float z) Position { get; }

    bool IsMoving { get; }

    void Say(string message);
    void SayLocalized(int stringId);
    void MoveTo(float x, float y, float z, bool direct);

    /// <summary>
    /// Plays an animation (AnimationTypes.xml id); a looping one becomes the idle.
    /// </summary>
    void Emote(int animationId, bool loop);

    void Face(IScriptablePlayer player);

    /// <summary>
    /// Opens this NPC's conversation for the player, at the given node or its start node.
    /// </summary>
    void StartConversation(IScriptablePlayer player, int? nodeId);
    void SetHeading(float heading);

    void SetSpeed(float speed);

    IReadOnlyList<ulong> GetNearbyPlayerGuids(float radius);

    void PlayAnimation(int animationId);

    void SetBaseAnimation(int animationId);

    IReadOnlyList<ulong> GetPlayerGuidsOnTop(float horizontalRadius, float minHeight, float maxHeight);

    void SetCollidable(bool collidable);

    int AddEffectTag(int compositeEffectId);

    void RemoveEffectTag(int tagId);

    void PlayEffect(int compositeEffectId);

    void Remove(int compositeEffectId);

    /// <summary>
    /// Where the NPC was placed, for scripts that wander off and come back.
    /// </summary>
    (float x, float y, float z) HomePosition { get; }

    /// <summary>
    /// Players this NPC can see within the radius.
    /// </summary>
    IReadOnlyList<IScriptablePlayer> GetNearbyPlayers(float radius);

    /// <summary>
    /// A player this NPC can see, or null.
    /// </summary>
    IScriptablePlayer? GetPlayer(ulong guid);

    /// <summary>
    /// Says something only the given player sees.
    /// </summary>
    void SayTo(IScriptablePlayer player, string message);

    void SetScale(float scale);

    /// <summary>
    /// Changes the nameplate and chat name.
    /// </summary>
    void Rename(string name);

    /// <summary>
    /// Runs onSignal(name, data, fromGuid) on other scripted NPCs in the zone within the radius (0 for the
    /// whole zone). Returns false if this NPC has already sent too many signals this second.
    /// </summary>
    bool Signal(string name, string? data, float radius);

    /// <summary>
    /// A hostile world NPC that fights (see Mobs.json). The rest of the mob members are 0 or false otherwise.
    /// </summary>
    bool IsMob { get; }
    int Health { get; }
    int MaxHealth { get; }
    bool IsDead { get; }

    /// <summary>
    /// The player the mob is fighting, or 0.
    /// </summary>
    ulong TargetGuid { get; }

    /// <summary>
    /// The mob takes damage from the player, as if they'd hit it. Returns what it took.
    /// </summary>
    int Hurt(IScriptablePlayer attacker, int damage);

    /// <summary>
    /// The NPC hits the player (mitigated like a mob's hit). Returns what the player took.
    /// </summary>
    int Attack(IScriptablePlayer player, int damage);
}
