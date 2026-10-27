using System.Collections.Generic;

using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Groups;

/// <summary>
/// Who is grouped with whom and who has been invited, by player guid. Not thread-safe (GroupManager locks
/// around it) and sends nothing, so the rules can be tested on their own.
/// </summary>
public sealed class GroupBook
{
    /// <summary>
    /// How long an invitation stays open.
    /// </summary>
    public const long InviteTimeoutMs = 60_000;

    private readonly record struct PendingInvite(ulong InviterGuid, long ExpiresAt);

    private readonly Dictionary<ulong, PlayerGroup> _groups = [];
    private readonly Dictionary<ulong, PendingInvite> _invites = [];

    public PlayerGroup? GetGroup(ulong guid) => _groups.GetValueOrDefault(guid);

    public GroupInviteResult Invite(ulong inviter, ulong invitee, long now)
    {
        if (inviter == invitee)
            return GroupInviteResult.NotFound;

        if (_groups.TryGetValue(inviter, out var group))
        {
            if (group.LeaderGuid != inviter)
                return GroupInviteResult.NotLeader;

            if (group.IsFull)
                return GroupInviteResult.GroupFull;
        }

        if (_groups.ContainsKey(invitee))
            return GroupInviteResult.AlreadyGrouped;

        if (_invites.TryGetValue(invitee, out var pending) && pending.ExpiresAt > now)
            return pending.InviterGuid == inviter ? GroupInviteResult.AlreadyInvited : GroupInviteResult.AlreadyHasInvite;

        _invites[invitee] = new PendingInvite(inviter, now + InviteTimeoutMs);

        return GroupInviteResult.Sent;
    }

    /// <summary>
    /// Takes up the invitation, starting the inviter's group if they had none. <paramref name="inviter"/> is
    /// who sent it (0 when there was none); <paramref name="group"/> is set when <see cref="GroupAcceptResult.Joined"/>.
    /// </summary>
    public GroupAcceptResult Accept(ulong invitee, long now, out ulong inviter, out PlayerGroup? group)
    {
        group = null;
        inviter = TakeInvite(invitee, now);

        if (inviter == 0)
            return GroupAcceptResult.NotInvited;

        // They joined another group (theirs or someone else's) since the invitation.
        if (_groups.ContainsKey(invitee))
            return GroupAcceptResult.NoGroup;

        if (_groups.TryGetValue(inviter, out var inviterGroup))
        {
            if (inviterGroup.LeaderGuid != inviter)
                return GroupAcceptResult.NoGroup;

            if (inviterGroup.IsFull)
                return GroupAcceptResult.NoRoom;
        }
        else
        {
            inviterGroup = new PlayerGroup();
            inviterGroup.Add(inviter);
            _groups[inviter] = inviterGroup;
        }

        inviterGroup.Add(invitee);
        _groups[invitee] = inviterGroup;

        group = inviterGroup;
        return GroupAcceptResult.Joined;
    }

    /// <summary>
    /// Turns the invitation down. Returns who sent it, or 0 if there was none.
    /// </summary>
    public ulong Decline(ulong invitee, long now) => TakeInvite(invitee, now);

    /// <summary>
    /// Takes <paramref name="member"/> out of their group. Returns that group, or null if they had none.
    /// A group left with one player breaks up: <paramref name="disbandedGuid"/> is that last player.
    /// </summary>
    public PlayerGroup? Leave(ulong member, out ulong disbandedGuid)
    {
        disbandedGuid = 0;

        if (!_groups.Remove(member, out var group))
            return null;

        group.Remove(member);

        if (group.Members.Count == 1)
        {
            disbandedGuid = group.Members[0];
            _groups.Remove(disbandedGuid);
            group.Remove(disbandedGuid);
        }

        return group;
    }

    public GroupKickResult Kick(ulong leader, ulong target, out PlayerGroup? group, out ulong disbandedGuid)
    {
        disbandedGuid = 0;
        group = GetGroup(leader);

        if (group is null)
            return GroupKickResult.NotInGroup;

        if (group.LeaderGuid != leader)
            return GroupKickResult.NotOwner;

        if (target == leader || !group.Contains(target))
            return GroupKickResult.TargetNotInGroup;

        Leave(target, out disbandedGuid);

        return GroupKickResult.Kicked;
    }

    /// <summary>
    /// Drops every invitation to or from <paramref name="guid"/>, e.g. when they log out.
    /// </summary>
    public void ForgetInvites(ulong guid)
    {
        _invites.Remove(guid);

        foreach (var (invitee, pending) in _invites)
        {
            if (pending.InviterGuid == guid)
                _invites.Remove(invitee);
        }
    }

    private ulong TakeInvite(ulong invitee, long now)
    {
        if (!_invites.Remove(invitee, out var pending) || pending.ExpiresAt <= now)
            return 0;

        return pending.InviterGuid;
    }
}
