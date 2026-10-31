using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Sent after every list of the player's own houses (no candidate: all of them) and every house the player enters
/// (that one), for its stars, votes and vote button.
/// </summary>
public class RatingPacketCandidateInfoRequest : BaseRatingPacket, IDeserializable<RatingPacketCandidateInfoRequest>
{
    public new const byte OpCode = 16;

    public string System = string.Empty;

    /// <summary>
    /// A house guid in decimal, or empty for every house of <see cref="OwnerGuid"/>.
    /// </summary>
    public string CandidateId = string.Empty;

    public ulong OwnerGuid;
    public ulong PlayerGuid;

    public RatingPacketCandidateInfoRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out RatingPacketCandidateInfoRequest value)
    {
        value = new RatingPacketCandidateInfoRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader) || !reader.TryRead(out value.System) || !reader.TryRead(out value.CandidateId) ||
            !reader.TryRead(out value.OwnerGuid) || !reader.TryRead(out value.PlayerGuid))
            return false;

        return reader.RemainingLength == 0;
    }
}
