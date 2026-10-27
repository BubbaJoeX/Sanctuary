using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Changes a row's count; 0 removes it.
/// </summary>
public class TradeUpdateItemCountPacket : BaseTradePacket, ISerializablePacket
{
    public new const byte OpCode = 13;

    public int SessionId;
    public int Revision;
    public bool IsMine;
    public int ItemId;
    public int Count;

    public TradeUpdateItemCountPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(SessionId);
        writer.Write(Revision);
        writer.Write(IsMine);
        writer.Write(ItemId);
        writer.Write(Count);

        return writer.Buffer;
    }
}
