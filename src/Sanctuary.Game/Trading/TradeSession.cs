using System.Collections.Generic;
using System.Threading;

namespace Sanctuary.Game.Trading;

/// <summary>
/// One player's half of a trade.
/// </summary>
public sealed class TradeSide
{
    public TradeSide(ulong guid)
    {
        Guid = guid;
    }

    public ulong Guid { get; }

    /// <summary>
    /// Offered inventory items (ClientItem.Id) and how many of each.
    /// </summary>
    public Dictionary<int, int> Items { get; } = [];

    public int Coins { get; set; }

    /// <summary>
    /// Locked in at the session's current revision ("this is my offer").
    /// </summary>
    public bool Locked { get; set; }

    /// <summary>
    /// Pressed Accept in the confirmation window.
    /// </summary>
    public bool Accepted { get; set; }
}

public enum TradeOfferChange
{
    Unchanged,
    Added,
    Changed,
    Removed
}

/// <summary>
/// A trade between two players: an invite until the invitee says yes, then both offers. Any change to an
/// offer raises <see cref="Revision"/> and unlocks both sides, so nobody accepts something they didn't see.
/// Not thread-safe (TradeManager locks around it) and sends nothing.
/// </summary>
public sealed class TradeSession
{
    private static int _lastId;

    public TradeSession(ulong inviterGuid, ulong inviteeGuid)
    {
        Inviter = new TradeSide(inviterGuid);
        Invitee = new TradeSide(inviteeGuid);
    }

    public int Id { get; } = Interlocked.Increment(ref _lastId);

    public TradeSide Inviter { get; }
    public TradeSide Invitee { get; }

    public bool Started { get; private set; }

    /// <summary>
    /// The client starts at 1 when the window opens and echoes the latest one it saw with each lock and accept.
    /// </summary>
    public int Revision { get; private set; } = 1;

    public bool BothLocked => Inviter.Locked && Invitee.Locked;
    public bool BothAccepted => Inviter.Accepted && Invitee.Accepted;

    public void Start() => Started = true;

    public TradeSide? GetSide(ulong guid) => guid == Inviter.Guid ? Inviter : guid == Invitee.Guid ? Invitee : null;

    public TradeSide? GetPartner(ulong guid) => guid == Inviter.Guid ? Invitee : guid == Invitee.Guid ? Inviter : null;

    /// <summary>
    /// Sets how many of an item <paramref name="guid"/> offers. Returns what that did to their side of the window.
    /// </summary>
    public TradeOfferChange SetItem(ulong guid, int itemId, int count)
    {
        var side = GetSide(guid);

        if (side is null || !Started || count < 0)
            return TradeOfferChange.Unchanged;

        var had = side.Items.TryGetValue(itemId, out var current);

        if (count == 0)
        {
            if (!had)
                return TradeOfferChange.Unchanged;

            side.Items.Remove(itemId);
            OnOfferChanged();

            return TradeOfferChange.Removed;
        }

        if (had && current == count)
            return TradeOfferChange.Unchanged;

        side.Items[itemId] = count;
        OnOfferChanged();

        return had ? TradeOfferChange.Changed : TradeOfferChange.Added;
    }

    public bool SetCoins(ulong guid, int coins)
    {
        var side = GetSide(guid);

        if (side is null || !Started || coins < 0 || side.Coins == coins)
            return false;

        side.Coins = coins;
        OnOfferChanged();

        return true;
    }

    /// <summary>
    /// Locks or unlocks <paramref name="guid"/>'s offer. Locking needs the current <paramref name="revision"/>.
    /// </summary>
    public bool SetLocked(ulong guid, int revision, bool locked)
    {
        var side = GetSide(guid);

        if (side is null || !Started || locked && revision != Revision)
            return false;

        side.Locked = locked;

        if (!locked)
            side.Accepted = false;

        return true;
    }

    /// <summary>
    /// Accepts both offers as of <paramref name="revision"/>; only once both are locked at it.
    /// </summary>
    public bool Accept(ulong guid, int revision)
    {
        var side = GetSide(guid);

        if (side is null || !BothLocked || revision != Revision)
            return false;

        side.Accepted = true;

        return true;
    }

    private void OnOfferChanged()
    {
        Revision++;

        Inviter.Locked = Invitee.Locked = false;
        Inviter.Accepted = Invitee.Accepted = false;
    }
}
