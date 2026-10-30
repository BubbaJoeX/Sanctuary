using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The battle could not start: a locale string, printed in chat with {player} as the player's name.
/// </summary>
public class EncounterPacketEncounterLaunchFailed : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 4;

    public int MessageId;

    public EncounterPacketEncounterLaunchFailed() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(MessageId);

        return writer.Buffer;
    }
}
