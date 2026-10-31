using Sanctuary.Game.Entities;

namespace Sanctuary.Game.Housing;

public interface IHouseRatingManager
{
    /// <summary>
    /// A house's rating (or, with no candidate, every listed house of the player's) for its side panel.
    /// </summary>
    void SendCandidateInfo(Player player, string candidateId, ulong ownerGuid);

    /// <summary>
    /// Lists the player's house in the Housing Directory.
    /// </summary>
    void Publish(Player player, string candidateId, string description, string keywords);

    /// <summary>
    /// Takes the player's house out of the Housing Directory, and its votes with it.
    /// </summary>
    void Unpublish(Player player, string candidateId);

    void Vote(Player player, string candidateId, float stars);

    /// <summary>
    /// A Housing Directory tab: Popular, Friends, New or any.
    /// </summary>
    void SendList(Player player, int requestType);

    void Search(Player player, string term);

    void SendFeatured(Player player);
}
