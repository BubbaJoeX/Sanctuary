using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// A house's rating as its side panel shows it: the stars, the votes, whether it's in the directory and whether the
/// player can vote.
/// </summary>
public class RatingCandidateInfo : ISerializableType
{
    public string CandidateId { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;

    public float Rating { get; set; }
    public int NumVotes { get; set; }

    /// <summary>
    /// In the Housing Directory: visitors get the vote panel.
    /// </summary>
    public bool HasRating { get; set; }

    public bool CanVote { get; set; }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(CandidateId);
        writer.Write(Description);
        writer.Write(Keywords);
        writer.Write(Rating);
        writer.Write(NumVotes);
        writer.Write(HasRating);
        writer.Write(CanVote);
    }
}
