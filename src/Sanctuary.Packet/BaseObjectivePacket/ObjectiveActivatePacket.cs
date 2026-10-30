using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Starts one of the running minigame's goals, out of Total.
/// </summary>
public class ObjectiveActivatePacket : BaseObjectivePacket, ISerializablePacket
{
    public new const byte OpCode = 1;

    public int ObjectiveId;

    public int Total;

    public ObjectiveActivatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ObjectiveId);
        writer.Write(Total);

        return writer.Buffer;
    }
}
