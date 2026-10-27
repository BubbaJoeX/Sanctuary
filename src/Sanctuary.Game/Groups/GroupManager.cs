using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Groups;

public sealed class GroupManager : IGroupManager
{
    private readonly ILogger _logger;
    private readonly IZoneManager _zoneManager;

    private readonly object _lock = new();
    private readonly GroupBook _book = new();

    public GroupManager(ILogger<GroupManager> logger, IZoneManager zoneManager)
    {
        _logger = logger;
        _zoneManager = zoneManager;
    }

    public void Invite(Player inviter, ulong inviteeGuid, string inviteeName)
    {
        if (!TryFindPlayer(inviteeGuid, inviteeName, out var invitee))
        {
            inviter.SendTunneled(new GroupPacketGroupInviteReply { Result = GroupInviteResult.NotFound });
            return;
        }

        GroupInviteResult result;
        int groupId;

        lock (_lock)
        {
            result = _book.Invite(inviter.Guid, invitee.Guid, Environment.TickCount64);
            groupId = _book.GetGroup(inviter.Guid)?.Id ?? 0;
        }

        inviter.SendTunneled(new GroupPacketGroupInviteReply { Result = result });

        if (result != GroupInviteResult.Sent)
            return;

        invitee.SendTunneled(new GroupPacketGroupInvite { Data = CreateInviteData(inviter, invitee, groupId) });
    }

    public void Respond(Player invitee, bool accepted)
    {
        var now = Environment.TickCount64;

        if (!accepted)
        {
            ulong inviterGuid;

            lock (_lock)
                inviterGuid = _book.Decline(invitee.Guid, now);

            // The inviter's client says the invitation was turned down.
            if (inviterGuid != 0 && _zoneManager.TryGetPlayer(inviterGuid, out var inviter))
                inviter.SendTunneled(new GroupPacketGroupAccept { Data = CreateInviteData(inviter, invitee, 0), Accepted = false });

            return;
        }

        GroupAcceptResult result;
        PlayerGroup? group;

        lock (_lock)
        {
            result = _book.Accept(invitee.Guid, now, out var inviterGuid, out group);

            // An invitation from someone who has since logged out leads nowhere.
            if (result == GroupAcceptResult.Joined && !_zoneManager.TryGetPlayer(inviterGuid, out _))
            {
                _book.Leave(invitee.Guid, out _);
                result = GroupAcceptResult.NoGroup;
                group = null;
            }

            if (group is not null)
                Track(group);
        }

        invitee.SendTunneled(new GroupPacketGroupAcceptReply { Result = result });

        if (group is null)
            return;

        foreach (var member in GetOnlineMembers(group).Where(member => member.Guid != invitee.Guid))
            ChatHelper.SendSystemMessage(member, $"{invitee.Name.FullName} joined the group.");

        SendRoster(group);

        _logger.LogInformation("{player} joined group {group} ({count} members).", invitee.Name.FullName, group.Id, group.Members.Count);
    }

    public void Leave(Player player)
    {
        PlayerGroup? group;
        ulong disbandedGuid;
        List<Player> recipients;

        lock (_lock)
        {
            group = _book.GetGroup(player.Guid);

            if (group is null)
                return;

            // Everyone still in it hears who left, the leaver included (their client drops the group).
            recipients = GetOnlineMembers(group);

            _book.Leave(player.Guid, out disbandedGuid);
            Untrack(player);
        }

        var leftPacket = new GroupPacketGroupLeave { Guid = player.Guid, Name = player.Name };

        foreach (var recipient in recipients)
            recipient.SendTunneled(leftPacket);

        FinishRemoval(group, disbandedGuid);
    }

    public void Kick(Player leader, ulong targetGuid, string targetName)
    {
        GroupKickResult result;
        PlayerGroup? group;
        ulong disbandedGuid;
        List<Player> recipients = [];
        Player? target = null;

        lock (_lock)
        {
            group = _book.GetGroup(leader.Guid);

            // The client sends the name; the guid is a fallback when it has one.
            target = group is null ? null : GetOnlineMembers(group).FirstOrDefault(member =>
                member.Guid == targetGuid || targetGuid == 0 && member.Name.FullName.Equals(targetName, StringComparison.OrdinalIgnoreCase));

            if (group is not null)
                recipients = GetOnlineMembers(group);

            result = _book.Kick(leader.Guid, target?.Guid ?? targetGuid, out group, out disbandedGuid);

            if (result == GroupKickResult.Kicked && target is not null)
                Untrack(target);
        }

        leader.SendTunneled(new GroupPacketGroupKickReply { Result = result });

        if (result != GroupKickResult.Kicked || group is null)
            return;

        var kickPacket = new GroupPacketGroupKick
        {
            Data = target is null
                ? new GroupInviteData { InviterGuid = leader.Guid, InviterName = leader.Name, InviteeGuid = targetGuid, GroupId = group.Id }
                : CreateInviteData(leader, target, group.Id)
        };

        foreach (var recipient in recipients)
            recipient.SendTunneled(kickPacket);

        FinishRemoval(group, disbandedGuid);
    }

    public void SendGroup(Player player)
    {
        var group = player.Group;

        if (group is not null)
            player.SendTunneled(CreateRoster(group));
    }

    public void OnLogout(Player player)
    {
        Leave(player);

        lock (_lock)
            _book.ForgetInvites(player.Guid);
    }

    /// <summary>
    /// After someone left or was kicked: a group of one breaks up, anything bigger gets its new roster.
    /// </summary>
    private void FinishRemoval(PlayerGroup group, ulong disbandedGuid)
    {
        if (disbandedGuid != 0)
        {
            if (!_zoneManager.TryGetPlayer(disbandedGuid, out var last))
                return;

            lock (_lock)
                Untrack(last);

            last.SendTunneled(new GroupPacketGroupLeave { Guid = last.Guid, Name = last.Name });
            ChatHelper.SendSystemMessage(last, "Your group has disbanded.");

            return;
        }

        SendRoster(group);
    }

    private void Track(PlayerGroup group)
    {
        foreach (var member in GetOnlineMembers(group))
            member.Group = group;

        group.MemberChanged -= SendRoster;
        group.MemberChanged += SendRoster;
    }

    private static void Untrack(Player player) => player.Group = null;

    private void SendRoster(PlayerGroup group)
    {
        var roster = CreateRoster(group);

        foreach (var member in GetOnlineMembers(group))
            member.SendTunneled(roster);
    }

    private GroupPacketGroupUpdate CreateRoster(PlayerGroup group)
    {
        var roster = new GroupPacketGroupUpdate();

        foreach (var member in GetOnlineMembers(group))
        {
            var profile = member.ActiveProfile;

            roster.Members.Add(new GroupMember
            {
                Guid = member.Guid,
                Name = member.Name,
                ProfileId = profile.Id,
                ProfileIconId = profile.Icon,
                ProfileBackgroundIconId = profile.BadgeImageSet,
                ProfileRank = profile.Rank
            });
        }

        return roster;
    }

    // Leader first, as the roster's order is what makes the leader.
    private List<Player> GetOnlineMembers(PlayerGroup group)
    {
        List<Player> members = [];

        foreach (var guid in group.Members)
        {
            if (_zoneManager.TryGetPlayer(guid, out var member))
                members.Add(member);
        }

        return members;
    }

    private bool TryFindPlayer(ulong guid, string name, out Player player)
    {
        if (guid != 0 && _zoneManager.TryGetPlayer(guid, out player!))
            return true;

        return _zoneManager.TryGetPlayer(name.Trim(), out player!);
    }

    private static GroupInviteData CreateInviteData(Player inviter, Player invitee, int groupId) => new()
    {
        InviterGuid = inviter.Guid,
        InviterName = inviter.Name,
        InviteeGuid = invitee.Guid,
        InviteeName = invitee.Name,
        GroupId = groupId
    };
}
