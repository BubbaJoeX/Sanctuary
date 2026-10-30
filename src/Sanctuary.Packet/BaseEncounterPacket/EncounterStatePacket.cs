using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The server's state for the battle. The client only acts on 9 (drops it from the running list); live sent 2, 3
/// and 4 with the offer, 5 when the instance was up and 6 as the player went in.
/// </summary>
public class EncounterStatePacket : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 106;

    public int State;

    public EncounterStatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(State);

        return writer.Buffer;
    }
}
