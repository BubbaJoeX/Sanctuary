using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// "Submit to the Housing Directory". The client leaves the owner's name, the picture, the description and the
/// keywords empty; the server knows the house.
/// </summary>
public class RatingPacketAddCandidate : BaseRatingPacket, IDeserializable<RatingPacketAddCandidate>
{
    public new const byte OpCode = 6;

    public string System = string.Empty;
    public string CandidateId = string.Empty;
    public string OwnerName = string.Empty;
    public string Name = string.Empty;
    public ulong OwnerGuid;
    public string Snapshot = string.Empty;
    public string Description = string.Empty;
    public string Keywords = string.Empty;

    public RatingPacketAddCandidate() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out RatingPacketAddCandidate value)
    {
        value = new RatingPacketAddCandidate();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader) || !reader.TryRead(out value.System) || !reader.TryRead(out value.CandidateId) ||
            !reader.TryRead(out value.OwnerName) || !reader.TryRead(out value.Name) || !reader.TryRead(out value.OwnerGuid) ||
            !reader.TryRead(out value.Snapshot) || !reader.TryRead(out value.Description) || !reader.TryRead(out value.Keywords))
            return false;

        return reader.RemainingLength == 0;
    }
}
