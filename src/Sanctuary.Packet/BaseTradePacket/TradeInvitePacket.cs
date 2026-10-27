using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Shows the invitee "Trade with [player]?". Dropped by a client already trading or invited.
/// </summary>
public class TradeInvitePacket : BaseTradePacket, ISerializablePacket
{
    public new const byte OpCode = 2;

    public NameData InviterName = new();
    public ulong InviterGuid;
    public int SessionId;

    public TradeInvitePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        InviterName.Serialize(writer);
        writer.Write(InviterGuid);
        writer.Write(SessionId);

        return writer.Buffer;
    }
}
