using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class TaskUpdatePacket : BaseUiPacket, ISerializablePacket
{
    public new const byte OpCode = 2;

    public int TaskId;

    public int Count;

    public TaskUpdatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(TaskId);
        writer.Write(Count);

        return writer.Buffer;
    }
}
