using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Shows a group member as playing a minigame (or not) in the group window.
/// </summary>
public class GroupPacketInMinigame : BaseGroupPacket, ISerializablePacket
{
    public new const short OpCode = 17;

    public ulong MemberGuid;

    public bool InMinigame;

    public GroupPacketInMinigame() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(MemberGuid);
        writer.Write(InMinigame);

        return writer.Buffer;
    }
}
