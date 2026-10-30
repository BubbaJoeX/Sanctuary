using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ObjectiveFailPacket : BaseObjectivePacket, ISerializablePacket
{
    public new const byte OpCode = 4;

    public int ObjectiveId;

    public ObjectiveFailPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ObjectiveId);

        return writer.Buffer;
    }
}
