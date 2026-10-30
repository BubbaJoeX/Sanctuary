using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A group battle: the group window's accept timer and a notification naming the battle. Timeout 0 closes the
/// timer. Answered with <see cref="EncounterInvitationResponsePacket"/>.
/// </summary>
public class EncounterInvitationPacket : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 102;

    /// <summary>
    /// Not read by the client.
    /// </summary>
    public ulong Guid;

    public int NameId;

    /// <summary>
    /// Seconds.
    /// </summary>
    public int Timeout;

    public EncounterInvitationPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(NameId);
        writer.Write(Timeout);

        return writer.Buffer;
    }
}
