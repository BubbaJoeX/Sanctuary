using System.Collections.Generic;

namespace Sanctuary.Scripting;

/// <summary>
/// What GM-authored scripts can see and do with a player (see <see cref="ScriptSandbox"/>).
/// </summary>
public interface IScriptablePlayer
{
    ulong Guid { get; }
    string Name { get; }
    (float x, float y, float z) Position { get; }

    /// <summary>
    /// Sends a system chat line to this player only.
    /// </summary>
    void Message(string message);

    void GiveItem(int itemDefinitionId, int quantity);

    /// <summary>
    /// Starts a quest for the player if it's offerable to them (prerequisites met, not already taken).
    /// </summary>
    void AcceptQuest(int questId);

    bool HasCompletedQuest(int questId);

    /// <summary>
    /// The player is an admin or moderator.
    /// </summary>
    bool IsGm { get; }

    bool IsMounted { get; }

    void Dismount();

    /// <summary>
    /// Moves the player within their zone, keeping their facing unless a heading is given.
    /// </summary>
    void Teleport(float x, float y, float z, float? heading);

    void GiveCoins(int amount);

    void Heal(int amount);

    void PlayEffect(int compositeEffectId);

    /// <summary>
    /// Plays an animation (AnimationTypes.xml id) on the player for everyone nearby.
    /// </summary>
    void Emote(int animationId);

    /// <summary>
    /// Turns the player into another model for a while.
    /// </summary>
    void Transform(int modelId, int seconds);

    void ClearTransform();

    /// <summary>
    /// Grows or shrinks the player for a while.
    /// </summary>
    void Resize(float scale, int seconds);

    /// <summary>
    /// The zone the player is in.
    /// </summary>
    int ZoneId { get; }
    string ZoneName { get; }

    int Health { get; }
    int MaxHealth { get; }

    /// <summary>
    /// Knocked out by a mob (0 hit points), until they recover at the zone start.
    /// </summary>
    bool IsKnockedOut { get; }

    /// <summary>
    /// Hit or hitting a mob in the last few seconds.
    /// </summary>
    bool IsInCombat { get; }

    /// <summary>
    /// The active job (profile id) and its rank.
    /// </summary>
    int Job { get; }
    int JobRank { get; }

    int Coins { get; }

    /// <summary>
    /// The player's group, or 0 when they aren't in one.
    /// </summary>
    int GroupId { get; }

    bool IsGroupLeader { get; }

    /// <summary>
    /// Everyone in the player's group, the leader first, wherever they are; empty when not grouped.
    /// </summary>
    IReadOnlyList<ulong> GroupMemberGuids { get; }

    /// <summary>
    /// How many of an item the player carries, across tints.
    /// </summary>
    int CountItem(int itemDefinitionId);

    /// <summary>
    /// Removes that many of an item. False, taking nothing, if the player has fewer.
    /// </summary>
    bool TakeItem(int itemDefinitionId, int quantity);

    /// <summary>
    /// Removes coins. False, taking nothing, if the player has fewer.
    /// </summary>
    bool TakeCoins(int amount);

    /// <summary>
    /// Adds stars to the active job, ranking it up as the bar fills. False at the top rank.
    /// </summary>
    bool GiveStars(int amount);

    /// <summary>
    /// Sends the player to a world zone, at the given spot or the zone's start. False if it isn't a world zone.
    /// </summary>
    bool Warp(int zoneId, (float x, float y, float z)? position, float? heading);
}
