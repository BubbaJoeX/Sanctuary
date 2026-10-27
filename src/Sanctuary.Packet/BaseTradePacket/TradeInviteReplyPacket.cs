using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The invitee's answer to a trade invite.
/// </summary>
public class TradeInviteReplyPacket : BaseTradePacket, IDeserializable<TradeInviteReplyPacket>
{
    public new const byte OpCode = 3;

    public int SessionId;
    public bool Accepted;

    public TradeInviteReplyPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out TradeInviteReplyPacket value)
    {
        value = new TradeInviteReplyPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.SessionId))
            return false;

        if (!reader.TryRead(out value.Accepted))
            return false;

        return reader.RemainingLength == 0;
    }
}
