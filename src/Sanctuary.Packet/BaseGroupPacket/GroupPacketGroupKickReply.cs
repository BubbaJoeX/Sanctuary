using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Tells the leader, after a kick, how it went.
/// </summary>
public class GroupPacketGroupKickReply : BaseGroupPacket, ISerializablePacket
{
    public new const short OpCode = 7;

    public GroupKickResult Result;

    public GroupPacketGroupKickReply() : base(OpCode)
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
