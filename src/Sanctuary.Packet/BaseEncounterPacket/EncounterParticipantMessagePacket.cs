using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A locale string in chat; {invited_player} is the named actor's name if it's in view, else Name.
/// </summary>
public class EncounterParticipantMessagePacket : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 120;

    public int MessageId;

    public ulong Guid;

    public string Name = string.Empty;

    public EncounterParticipantMessagePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(MessageId);
        writer.Write(Guid);
        writer.Write(Name);

        return writer.Buffer;
    }
}
