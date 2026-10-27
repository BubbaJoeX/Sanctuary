using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Accepts both offers, from the confirmation window. Revision is the offer it was shown.
/// </summary>
public class TradeAcceptPacket : BaseTradePacket, IDeserializable<TradeAcceptPacket>
{
    public new const byte OpCode = 6;

    public int SessionId;
    public int Revision;

    public TradeAcceptPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out TradeAcceptPacket value)
    {
        value = new TradeAcceptPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.SessionId))
            return false;

        if (!reader.TryRead(out value.Revision))
            return false;

        return reader.RemainingLength == 0;
    }
}
