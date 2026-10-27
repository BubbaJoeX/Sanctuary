using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// Who invited whom, as group invites, accepts and kicks carry it. The client fills in only
/// <see cref="InviteeName"/>'s first name (whatever was typed or clicked) when it sends an invite.
/// </summary>
public class GroupInviteData : ISerializableType
{
    public ulong InviterGuid;
    public NameData InviterName = new();

    public ulong InviteeGuid;
    public NameData InviteeName = new();

    public int GroupId;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(InviterGuid);
        InviterName.Serialize(writer);

        writer.Write(InviteeGuid);
        InviteeName.Serialize(writer);

        writer.Write(GroupId);
    }

    public bool TryRead(ref PacketReader reader)
    {
        return reader.TryRead(out InviterGuid)
            && InviterName.TryRead(ref reader)
            && reader.TryRead(out InviteeGuid)
            && InviteeName.TryRead(ref reader)
            && reader.TryRead(out GroupId);
    }
}
