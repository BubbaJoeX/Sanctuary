namespace Sanctuary.Packet.Common;

/// <summary>
/// GroupPacketGroupInviteReply results, each a client message (GroupInviteSent, GroupInviteFailNotFound...).
/// </summary>
public enum GroupInviteResult
{
    Sent = 0,
    NotFound = 1,
    AlreadyGrouped = 3,
    AlreadyHasInvite = 4,
    NotLeader = 5,
    AlreadyInvited = 6,
    GroupFull = 7
}

/// <summary>
/// GroupPacketGroupAcceptReply results (GroupAcceptFailNoGroup, GroupAcceptFailNoRoom, GroupAcceptFailNotInvited).
/// </summary>
public enum GroupAcceptResult
{
    Joined = 0,
    NoGroup = 1,
    NoRoom = 2,
    NotInvited = 3
}

/// <summary>
/// GroupPacketGroupKickReply results (GroupKickFailNotInGroup, GroupKickFailNotOwner, GroupKickFailTargetNotInGroup).
/// </summary>
public enum GroupKickResult
{
    Kicked = 0,
    NotInGroup = 1,
    NotOwner = 2,
    TargetNotInGroup = 3
}
