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
}
