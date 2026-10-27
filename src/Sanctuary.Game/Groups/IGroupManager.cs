using Sanctuary.Game.Entities;

namespace Sanctuary.Game.Groups;

/// <summary>
/// Groups (parties): invites, joining, leaving, kicks, and keeping every member's group window current.
/// </summary>
public interface IGroupManager
{
    /// <summary>
    /// <paramref name="inviter"/> asks a player to join, found by guid or else by name.
    /// </summary>
    void Invite(Player inviter, ulong inviteeGuid, string inviteeName);

    /// <summary>
    /// The answer to <paramref name="invitee"/>'s open invitation.
    /// </summary>
    void Respond(Player invitee, bool accepted);

    void Leave(Player player);

    /// <summary>
    /// The leader removes a member, found by guid or else by name.
    /// </summary>
    void Kick(Player leader, ulong targetGuid, string targetName);

    /// <summary>
    /// Resends <paramref name="player"/>'s group window, e.g. after a zone load.
    /// </summary>
    void SendGroup(Player player);

    /// <summary>
    /// Logging out leaves the group and drops any open invitations.
    /// </summary>
    void OnLogout(Player player);
}
