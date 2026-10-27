using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Tells the inviter, after an invite, how it went.
/// </summary>
public class GroupPacketGroupInviteReply : BaseGroupPacket, ISerializablePacket
{
    public new const short OpCode = 2;

    public GroupInviteResult Result;

    public GroupPacketGroupInviteReply() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write((int)Result);

        return writer.Buffer;
    }
}
