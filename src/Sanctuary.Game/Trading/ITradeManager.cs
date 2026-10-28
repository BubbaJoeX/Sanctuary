using Sanctuary.Game.Entities;

namespace Sanctuary.Game.Trading;

/// <summary>
/// Player-to-player trades of items and coins, driven by the client's trade window.
/// </summary>
public interface ITradeManager
{
    /// <summary>
    /// <paramref name="inviter"/> picked Trade on <paramref name="invitee"/>'s menu.
    /// </summary>
    void Invite(Player inviter, Player invitee);

    void ReplyToInvite(Player invitee, int sessionId, bool accepted);

    void OfferItem(Player player, int sessionId, int itemId, int count);

    void OfferCoins(Player player, int sessionId, int coins);

    void Lock(Player player, int sessionId, int revision, bool locked);

    void Accept(Player player, int sessionId, int revision);

    void Cancel(Player player, int sessionId);

    /// <summary>
    /// Logging out ends any trade or invitation, telling the partner they left.
    /// </summary>
    void OnLogout(Player player);
}
