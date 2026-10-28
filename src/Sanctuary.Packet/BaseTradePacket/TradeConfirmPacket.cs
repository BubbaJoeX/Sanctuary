using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Both offers are locked: starts the confirmation window's timer before Accept can be pressed.
/// </summary>
public class TradeConfirmPacket : BaseTradePacket, ISerializablePacket
{
    public new const byte OpCode = 18;

    public int SessionId;

    public TradeConfirmPacket() : base(OpCode)
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
