using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ObjectiveUpdatePacket : BaseObjectivePacket, ISerializablePacket
{
    public new const byte OpCode = 2;

    public int ObjectiveId;

    public int Count;

    public ObjectiveUpdatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ObjectiveId);
        writer.Write(Count);

        return writer.Buffer;
    }
}
