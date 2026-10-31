using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A visitor's vote on the house side panel: the star they picked, 1 to 5.
/// </summary>
public class RatingPacketAddVote : BaseRatingPacket, IDeserializable<RatingPacketAddVote>
{
    public new const byte OpCode = 8;

    public string System = string.Empty;
    public string CandidateId = string.Empty;

    /// <summary>
    /// Always empty from this client.
    /// </summary>
    public string Unknown = string.Empty;

    /// <summary>
    /// The voter's guid, in decimal.
    /// </summary>
    public string VoterGuid = string.Empty;

    public float Vote;

    public RatingPacketAddVote() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out RatingPacketAddVote value)
    {
        value = new RatingPacketAddVote();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader) || !reader.TryRead(out value.System) || !reader.TryRead(out value.CandidateId) ||
            !reader.TryRead(out value.Unknown) || !reader.TryRead(out value.VoterGuid) || !reader.TryRead(out value.Vote))
            return false;

        return reader.RemainingLength == 0;
    }
}
