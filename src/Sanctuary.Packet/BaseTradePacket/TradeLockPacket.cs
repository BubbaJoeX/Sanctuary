using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Locks in (or takes back) this player's offer at Revision, which opens their confirmation window.
/// </summary>
public class TradeLockPacket : BaseTradePacket, IDeserializable<TradeLockPacket>
{
    public new const byte OpCode = 8;

    public int SessionId;
    public int Revision;
    public bool Locked;

    public TradeLockPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out TradeLockPacket value)
    {
        value = new TradeLockPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.SessionId))
            return false;

        if (!reader.TryRead(out value.Revision))
            return false;

        if (!reader.TryRead(out value.Locked))
            return false;

        return reader.RemainingLength == 0;
    }
}
