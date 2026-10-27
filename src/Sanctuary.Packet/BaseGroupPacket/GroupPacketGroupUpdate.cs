using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// The whole group, which the client rebuilds its group window from. The first member is the leader;
/// a group of one shows as "Solo".
/// </summary>
public class GroupPacketGroupUpdate : BaseGroupPacket, ISerializablePacket
{
    public new const short OpCode = 8;

    public List<GroupMember> Members = [];

    public GroupPacketGroupUpdate() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Members.Count);

        foreach (var member in Members)
            member.Serialize(writer);

        return writer.Buffer;
    }
}
