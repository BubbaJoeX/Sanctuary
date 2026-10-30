using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The battle's instance is up: the start panel's Play button turns on.
/// </summary>
public class EncounterZoneIsReadyPacket : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 107;

    public EncounterZoneIsReadyPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        return writer.Buffer;
    }
}
