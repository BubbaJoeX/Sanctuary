using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class TaskFailPacket : BaseUiPacket, ISerializablePacket
{
    public new const byte OpCode = 4;

    public int TaskId;

    public TaskFailPacket() : base(OpCode)
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
