using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Opens the trade window, sent to both players with the other one as partner.
/// </summary>
public class TradeStartSessionPacket : BaseTradePacket, ISerializablePacket
{
    public new const byte OpCode = 4;

    public ulong PartnerGuid;
    public NameData PartnerName = new();
    public int SessionId;

    public TradeStartSessionPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PartnerGuid);
        PartnerName.Serialize(writer);
        writer.Write(SessionId);

        return writer.Buffer;
    }
}
