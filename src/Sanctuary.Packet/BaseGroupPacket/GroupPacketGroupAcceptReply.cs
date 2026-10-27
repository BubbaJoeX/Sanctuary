using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Tells the invitee, after accepting, how it went.
/// </summary>
public class GroupPacketGroupAcceptReply : BaseGroupPacket, ISerializablePacket
{
    public new const short OpCode = 5;

    public GroupAcceptResult Result;

    public GroupPacketGroupAcceptReply() : base(OpCode)
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
