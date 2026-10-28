using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// One of the client's trade messages, about the partner.
/// </summary>
public class TradeMessagePacket : BaseTradePacket, ISerializablePacket
{
    public new const byte OpCode = 15;

    public TradeMessageId MessageId;

    public TradeMessagePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write((byte)MessageId);

        return writer.Buffer;
    }
}
