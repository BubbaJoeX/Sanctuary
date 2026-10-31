using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The answer to the inventory's Use on a gift: a confirm with a title and a text, whose OK opens it. Without
/// <see cref="Success"/> the client's OK opens the old Station Cash store instead, so a gift that can't open gets a
/// message, not this.
/// </summary>
public class ClientUpdatePacketImmediateActivationCheck : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 30;

    public int ItemGuid;

    /// <summary>
    /// The store category and bundle the client shows when <see cref="Success"/> is off.
    /// </summary>
    public int CategoryId;
    public int BundleId;

    public int TitleStringId;
    public int BodyStringId;

    public bool Success = true;

    public ClientUpdatePacketImmediateActivationCheck() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ItemGuid);
        writer.Write(CategoryId);
        writer.Write(BundleId);
        writer.Write(TitleStringId);
        writer.Write(BodyStringId);
        writer.Write(Success);

        return writer.Buffer;
    }
}
