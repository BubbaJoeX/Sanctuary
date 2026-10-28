using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Closes the trade.
/// </summary>
public class TradeCancelPacket : BaseTradePacket, IDeserializable<TradeCancelPacket>
{
    public new const byte OpCode = 7;

    public int SessionId;

    public TradeCancelPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out TradeCancelPacket value)
    {
        value = new TradeCancelPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.SessionId))
            return false;

        return reader.RemainingLength == 0;
    }
}
