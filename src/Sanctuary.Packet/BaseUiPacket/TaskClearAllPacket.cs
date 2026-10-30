using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Empties the objective tracker and hides it.
/// </summary>
public class TaskClearAllPacket : BaseUiPacket, ISerializablePacket
{
    public new const byte OpCode = 5;

    public TaskClearAllPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        return writer.Buffer;
    }
}
