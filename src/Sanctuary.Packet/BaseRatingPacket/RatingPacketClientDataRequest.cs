using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The Housing Directory's Popular, Friends and New tabs (and Popular each time it opens). The houses it already
/// shows come with it.
/// </summary>
public class RatingPacketClientDataRequest : BaseRatingPacket, IDeserializable<RatingPacketClientDataRequest>
{
    public new const byte OpCode = 3;

    public const int General = 0;
    public const int Popular = 1;
    public const int Friends = 2;
    public const int New = 3;

    public string System = string.Empty;

    public int RequestType;

    public RatingPacketClientDataRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out RatingPacketClientDataRequest value)
    {
        value = new RatingPacketClientDataRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader) || !reader.TryRead(out value.System) || !reader.TryRead(out value.RequestType) ||
            !reader.TryRead(out int count) || count < 0)
            return false;

        // The houses it shows now (a hash, then the guid): not needed.
        for (var i = 0; i < count; i++)
        {
            if (!reader.TryRead(out int _) || !reader.TryRead(out string _))
                return false;
        }

        return reader.RemainingLength == 0;
    }
}
