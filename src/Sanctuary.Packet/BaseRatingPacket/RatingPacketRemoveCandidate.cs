using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// "Remove from the Housing Directory" (also sent when the owner picks up all their furniture): its votes are lost.
/// </summary>
public class RatingPacketRemoveCandidate : BaseRatingPacket, IDeserializable<RatingPacketRemoveCandidate>
{
    public new const byte OpCode = 7;

    public string System = string.Empty;
    public string CandidateId = string.Empty;

    public RatingPacketRemoveCandidate() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out RatingPacketRemoveCandidate value)
    {
        value = new RatingPacketRemoveCandidate();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader) || !reader.TryRead(out value.System) || !reader.TryRead(out value.CandidateId))
            return false;

        return reader.RemainingLength == 0;
    }
}
