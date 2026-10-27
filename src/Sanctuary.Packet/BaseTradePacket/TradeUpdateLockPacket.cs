using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The partner locked in their offer (OtherAccepted), or took it back, which also closes this player's confirmation.
/// </summary>
public class TradeUpdateLockPacket : BaseTradePacket, ISerializablePacket
{
    public new const byte OpCode = 17;

    public int SessionId;
    public bool Locked;

    public TradeUpdateLockPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(SessionId);
        writer.Write(Locked);

        return writer.Buffer;
    }
}
