using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Adds a row to one side of the window. The client doesn't merge rows, so send it once per item, to both players.
/// </summary>
public class TradeUpdateAddItemPacket : BaseTradePacket, ISerializablePacket
{
    public new const byte OpCode = 12;

    public int SessionId;
    public int Revision;
    public bool IsMine;
    public ItemInstance Item = new();

    public TradeUpdateAddItemPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(SessionId);
        writer.Write(Revision);
        writer.Write(IsMine);
        Item.Serialize(writer);

        return writer.Buffer;
    }
}
