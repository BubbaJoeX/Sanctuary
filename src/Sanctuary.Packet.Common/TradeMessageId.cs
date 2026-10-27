namespace Sanctuary.Packet.Common;

/// <summary>
/// TradeMessagePacket ids. [player] is the trade partner; Canceled, PartnerDisconnected and the instanced
/// zone messages also close an invite that hasn't been answered.
/// </summary>
public enum TradeMessageId : byte
{
    /// <summary>"[player] canceled the trade"</summary>
    Canceled = 1,

    /// <summary>"Trade canceled - [player] left the game"</summary>
    PartnerDisconnected = 2,

    /// <summary>"Trade canceled - your partner's offer was invalid"</summary>
    PartnerOfferInvalid = 3,

    /// <summary>"Trade canceled - your offer was invalid"</summary>
    YourOfferInvalid = 4,

    /// <summary>"Trade completed"</summary>
    Completed = 6,

    /// <summary>"Your trade has been canceled."</summary>
    YouEnteredInstancedZone = 7,

    /// <summary>"Your trade with [player] has been canceled."</summary>
    PartnerEnteredInstancedZone = 8,

    /// <summary>"Trade canceled - Your offer had an item that would exceed its max stack in your partner's inventory."</summary>
    YourOfferExceedsPartnersMaxStack = 9,

    /// <summary>"Trade canceled - Partner's offer had an item that would exceed its max stack in your inventory."</summary>
    PartnersOfferExceedsYourMaxStack = 10
}
