using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The Housing Directory's search box. Unlike the other requests, the player's guid comes before the system.
/// </summary>
public class RatingPacketSearchRequest : BaseRatingPacket, IDeserializable<RatingPacketSearchRequest>
{
    public new const byte OpCode = 12;

    public ulong PlayerGuid;

    public string System = string.Empty;
    public string SearchTerm = string.Empty;

    public RatingPacketSearchRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out RatingPacketSearchRequest value)
    {
        value = new RatingPacketSearchRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader) || !reader.TryRead(out value.PlayerGuid) || !reader.TryRead(out value.System) ||
            !reader.TryRead(out value.SearchTerm))
            return false;

        return reader.RemainingLength == 0;
    }
}
