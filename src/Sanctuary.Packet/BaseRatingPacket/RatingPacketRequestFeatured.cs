using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Sent as the Housing Directory opens, for its Featured House card. The client never sets the number after the
/// system.
/// </summary>
public class RatingPacketRequestFeatured : BaseRatingPacket, IDeserializable<RatingPacketRequestFeatured>
{
    public new const byte OpCode = 20;

    public string System = string.Empty;

    public RatingPacketRequestFeatured() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out RatingPacketRequestFeatured value)
    {
        value = new RatingPacketRequestFeatured();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader) || !reader.TryRead(out value.System) || !reader.TryRead(out ulong _))
            return false;

        return reader.RemainingLength == 0;
    }
}
