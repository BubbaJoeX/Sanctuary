using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Withdraws an offer: closes the start panel. The client reads but ignores the body.
/// </summary>
public class EncounterPacketEncounterShutdown : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 6;

    public ulong Guid;

    public int Unknown;

    public int Unknown2;

    public EncounterPacketEncounterShutdown() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Unknown);
        writer.Write(Unknown2);

        return writer.Buffer;
    }
}
