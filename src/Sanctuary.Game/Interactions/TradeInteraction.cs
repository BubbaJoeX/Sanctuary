using Sanctuary.Game.Entities;
using Sanctuary.Game.Trading;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Interactions;

/// <summary>
/// "Trade" on another player's menu. The client has no trade request of its own; the server sends the invite.
/// </summary>
public class TradeInteraction : IInteraction
{
    private readonly ITradeManager _tradeManager;

    public TradeInteraction(ITradeManager tradeManager)
    {
        _tradeManager = tradeManager;
    }

    public int Id => Data.Id;

    public static InteractionData Data = new()
    {
        Id = IInteraction.UniqueId++,
        IconId = 2416,
        ButtonText = 3299
    };

    public void OnInteract(Player player, IEntity other)
    {
        if (other is Player otherPlayer)
            _tradeManager.Invite(player, otherPlayer);
    }
}
