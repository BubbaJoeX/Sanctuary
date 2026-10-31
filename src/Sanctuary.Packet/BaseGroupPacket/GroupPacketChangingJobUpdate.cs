using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Shows a group member as changing job (or not) in the group window.
/// </summary>
public class GroupPacketChangingJobUpdate : BaseGroupPacket, ISerializablePacket
{
    public new const short OpCode = 15;

    public ulong MemberGuid;

    public bool IsChangingJob;

    public GroupPacketChangingJobUpdate() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(MemberGuid);
        writer.Write(IsChangingJob);

        return writer.Buffer;
    }
}
