using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Closes the trade window. Send any TradeMessagePacket first, as this clears the partner's name it uses.
/// </summary>
public class TradeEndSessionPacket : BaseTradePacket, ISerializablePacket
{
    public new const byte OpCode = 5;

    public int SessionId;

    public TradeEndSessionPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(SessionId);

        return writer.Buffer;
    }
}
