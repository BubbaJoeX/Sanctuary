using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Sets how many of an inventory item (ClientItem.Id) this player offers; the absolute total, 0 to take it back.
/// </summary>
public class TradeOfferItemPacket : BaseTradePacket, IDeserializable<TradeOfferItemPacket>
{
    public new const byte OpCode = 9;

    public int SessionId;
    public int ItemId;
    public int Count;

    public TradeOfferItemPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out TradeOfferItemPacket value)
    {
        value = new TradeOfferItemPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.SessionId))
            return false;

        if (!reader.TryRead(out value.ItemId))
            return false;

        if (!reader.TryRead(out value.Count))
            return false;

        return reader.RemainingLength == 0;
    }
}
