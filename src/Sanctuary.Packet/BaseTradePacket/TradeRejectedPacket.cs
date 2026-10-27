using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Tells the inviter "[player] doesn't want to trade".
/// </summary>
public class TradeRejectedPacket : BaseTradePacket, ISerializablePacket
{
    public new const byte OpCode = 16;

    public NameData Name = new();

    public TradeRejectedPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Name.Serialize(writer);

        return writer.Buffer;
    }
}
