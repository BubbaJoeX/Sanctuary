using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A member's answer to a group battle, ticked or crossed on everyone's group window.
/// </summary>
public class GroupPacketAnnounceEncounterReply : BaseGroupPacket, ISerializablePacket
{
    public new const short OpCode = 13;

    public ulong MemberGuid;

    public bool Accepted;

    public GroupPacketAnnounceEncounterReply() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(MemberGuid);
        writer.Write(Accepted);

        return writer.Buffer;
    }
}
