using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// A house card in the Housing Directory: the browser's lists, search results and the featured house.
/// </summary>
public class RatingCandidate : ISerializableType
{
    /// <summary>
    /// The house's guid in decimal. The card's Visit asks to enter it.
    /// </summary>
    public string CandidateId { get; set; } = string.Empty;

    public string OwnerName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public ulong OwnerGuid { get; set; }

    /// <summary>
    /// The card's picture: the client loads &lt;Snapshot&gt;_thumb.dds.
    /// </summary>
    public string Snapshot { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;

    public float Rating { get; set; }

    /// <summary>
    /// A float here, unlike in <see cref="RatingCandidateInfo"/>.
    /// </summary>
    public float NumVotes { get; set; }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(CandidateId);
        writer.Write(OwnerName);
        writer.Write(Name);
        writer.Write(OwnerGuid);
        writer.Write(Snapshot);
        writer.Write(Description);
        writer.Write(Keywords);
        writer.Write(Rating);
        writer.Write(NumVotes);
    }
}
