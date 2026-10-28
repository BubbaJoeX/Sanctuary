using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Sets how many coins this player offers (the absolute total).
/// </summary>
public class TradeCoinCountPacket : BaseTradePacket, IDeserializable<TradeCoinCountPacket>
{
    public new const byte OpCode = 10;

    public int SessionId;
    public int Coins;

    public TradeCoinCountPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out TradeCoinCountPacket value)
    {
        value = new TradeCoinCountPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.SessionId))
            return false;

        if (!reader.TryRead(out value.Coins))
            return false;

        return reader.RemainingLength == 0;
    }
}
