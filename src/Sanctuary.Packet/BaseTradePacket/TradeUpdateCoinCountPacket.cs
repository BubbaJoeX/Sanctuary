using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The partner's coin offer (IsMine false); the client ignores its own.
/// </summary>
public class TradeUpdateCoinCountPacket : BaseTradePacket, ISerializablePacket
{
    public new const byte OpCode = 11;

    public int SessionId;
    public int Revision;
    public bool IsMine;
    public int Coins;

    public TradeUpdateCoinCountPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(SessionId);
        writer.Write(Revision);
        writer.Write(IsMine);
        writer.Write(Coins);

        return writer.Buffer;
    }
}
