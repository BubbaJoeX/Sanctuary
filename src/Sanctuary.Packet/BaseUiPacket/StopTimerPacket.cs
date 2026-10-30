using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class StopTimerPacket : BaseUiPacket, ISerializablePacket
{
    public new const byte OpCode = 11;

    public StopTimerPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        return writer.Buffer;
    }
}
