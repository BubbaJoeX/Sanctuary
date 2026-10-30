using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A goal done: the "Objective Complete" toast (unless it's a bonus), its effect, and its points on the end
/// screen.
/// </summary>
public class ObjectiveCompletePacket : BaseObjectivePacket, ISerializablePacket
{
    public new const byte OpCode = 3;

    public int ObjectiveId;

    public int CompositeEffectId;

    public int Points;

    public ObjectiveCompletePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ObjectiveId);
        writer.Write(CompositeEffectId);
        writer.Write(Points);

        return writer.Buffer;
    }
}
