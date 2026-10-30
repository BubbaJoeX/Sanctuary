using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class TaskCompletePacket : BaseUiPacket, ISerializablePacket
{
    public new const byte OpCode = 3;

    public int TaskId;

    public TaskCompletePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(TaskId);

        return writer.Buffer;
    }
}
