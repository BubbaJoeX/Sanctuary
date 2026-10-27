using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Core.IO;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Trading;

public sealed class TradeManager : ITradeManager
{
    private readonly ILogger _logger;
    private readonly IZoneManager _zoneManager;
    private readonly IResourceManager _resourceManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    private readonly object _lock = new();
    private readonly Dictionary<ulong, TradeSession> _sessions = [];

    public TradeManager(ILogger<TradeManager> logger, IZoneManager zoneManager, IResourceManager resourceManager,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _logger = logger;
        _zoneManager = zoneManager;
        _resourceManager = resourceManager;
        _dbContextFactory = dbContextFactory;
    }

    public void Invite(Player inviter, Player invitee)
    {
        if (inviter.Guid == invitee.Guid)
            return;

        // An ignored player's invites are turned down without bothering the other side.
        if (invitee.Ignores.Any(x => x.Guid == inviter.Guid))
        {
            inviter.SendTunneled(new TradeRejectedPacket { Name = invitee.Name });
            return;
        }

        TradeSession session;

        lock (_lock)
        {
            // The client drops an invite while it's already trading or invited, so say so here instead.
            if (_sessions.ContainsKey(inviter.Guid))
            {
                ChatHelper.SendSystemMessage(inviter, "Finish your current trade first.");
                return;
            }

            if (_sessions.ContainsKey(invitee.Guid))
            {
                ChatHelper.SendSystemMessage(inviter, $"{invitee.Name.FullName} is busy with another trade.");
                return;
            }

            session = new TradeSession(inviter.Guid, invitee.Guid);

            _sessions[inviter.Guid] = session;
            _sessions[invitee.Guid] = session;
        }

        invitee.SendTunneled(new TradeInvitePacket
        {
            InviterName = inviter.Name,
            InviterGuid = inviter.Guid,
            SessionId = session.Id
        });

        ChatHelper.SendSystemMessage(inviter, $"You asked {invitee.Name.FullName} to trade.");
    }

    public void ReplyToInvite(Player invitee, int sessionId, bool accepted)
    {
        Player? inviter;
        TradeSession? session;

        lock (_lock)
        {
            if (!TryGetSession(invitee, sessionId, out session) || session.Started || session.Invitee.Guid != invitee.Guid)
                return;

            _zoneManager.TryGetPlayer(session.Inviter.Guid, out inviter);

            if (!accepted || inviter is null)
                End(session);
            else
                session.Start();
        }

        if (inviter is null)
        {
            // Closes the invite on the invitee's side; it would otherwise wait for a window that never opens.
            if (accepted)
                SendEnd(session, invitee, TradeMessageId.PartnerDisconnected);

            return;
        }

        if (!accepted)
        {
            inviter.SendTunneled(new TradeRejectedPacket { Name = invitee.Name });
            return;
        }

        inviter.SendTunneled(new TradeStartSessionPacket { PartnerGuid = invitee.Guid, PartnerName = invitee.Name, SessionId = session.Id });
        invitee.SendTunneled(new TradeStartSessionPacket { PartnerGuid = inviter.Guid, PartnerName = inviter.Name, SessionId = session.Id });

        _logger.LogInformation("{inviter} and {invitee} started trade {session}.", inviter.Name.FullName, invitee.Name.FullName, session.Id);
    }

    public void OfferItem(Player player, int sessionId, int itemId, int count)
    {
        lock (_lock)
        {
            if (!TryGetStartedSession(player, sessionId, out var session, out var partner))
                return;

            var clientItem = player.Items.FirstOrDefault(item => item.Id == itemId);

            if (count > 0 && !CanOffer(player, clientItem, count))
            {
                ChatHelper.SendSystemMessage(player, "That item can't be traded.");
                return;
            }

            var wasLocked = session.Inviter.Locked || session.Invitee.Locked;
            var change = session.SetItem(player.Guid, itemId, count);

            if (change == TradeOfferChange.Unchanged)
                return;

            if (change == TradeOfferChange.Added)
            {
                var row = new ItemInstance { Definition = clientItem!.Definition, Tint = clientItem.Tint, Id = itemId, Count = count };

                // The partner may never have seen this item, and the row shows its definition.
                if (_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var definition))
                    ItemDefinitionHelper.SendItemDefinition(partner, _resourceManager, definition);

                player.SendTunneled(new TradeUpdateAddItemPacket { SessionId = session.Id, Revision = session.Revision, IsMine = true, Item = row });
                partner.SendTunneled(new TradeUpdateAddItemPacket { SessionId = session.Id, Revision = session.Revision, IsMine = false, Item = row });
            }
            else
            {
                player.SendTunneled(new TradeUpdateItemCountPacket { SessionId = session.Id, Revision = session.Revision, IsMine = true, ItemId = itemId, Count = count });
                partner.SendTunneled(new TradeUpdateItemCountPacket { SessionId = session.Id, Revision = session.Revision, IsMine = false, ItemId = itemId, Count = count });
            }

            if (wasLocked)
                SendUnlocked(session, player, partner);
        }
    }

    public void OfferCoins(Player player, int sessionId, int coins)
    {
        lock (_lock)
        {
            if (!TryGetStartedSession(player, sessionId, out var session, out var partner))
                return;

            var wasLocked = session.Inviter.Locked || session.Invitee.Locked;

            if (!session.SetCoins(player.Guid, Math.Clamp(coins, 0, player.Coins)))
                return;

            partner.SendTunneled(new TradeUpdateCoinCountPacket
            {
                SessionId = session.Id,
                Revision = session.Revision,
                IsMine = false,
                Coins = session.GetSide(player.Guid)!.Coins
            });

            if (wasLocked)
                SendUnlocked(session, player, partner);
        }
    }

    public void Lock(Player player, int sessionId, int revision, bool locked)
    {
        lock (_lock)
        {
            if (!TryGetStartedSession(player, sessionId, out var session, out var partner))
                return;

            // A client told it's unlocked answers with its own unlock; passing that on would bounce forever.
            if (!locked && !session.GetSide(player.Guid)!.Locked)
                return;

            if (!session.SetLocked(player.Guid, revision, locked))
            {
                // Locked an offer that has since changed: close their confirmation so they look again.
                player.SendTunneled(new TradeUpdateLockPacket { SessionId = session.Id, Locked = false });
                return;
            }

            partner.SendTunneled(new TradeUpdateLockPacket { SessionId = session.Id, Locked = locked });

            if (session.BothLocked)
            {
                player.SendTunneled(new TradeConfirmPacket { SessionId = session.Id });
                partner.SendTunneled(new TradeConfirmPacket { SessionId = session.Id });
            }
        }
    }

    public void Accept(Player player, int sessionId, int revision)
    {
        lock (_lock)
        {
            if (!TryGetStartedSession(player, sessionId, out var session, out var partner))
                return;

            if (!session.Accept(player.Guid, revision) || !session.BothAccepted)
                return;

            End(session);

            var inviter = session.Inviter.Guid == player.Guid ? player : partner;
            var invitee = session.Inviter.Guid == player.Guid ? partner : player;

            var failure = Transfer(session, inviter, invitee);

            if (failure is null)
            {
                SendEnd(session, inviter, TradeMessageId.Completed);
                SendEnd(session, invitee, TradeMessageId.Completed);

                _logger.LogInformation("Trade {session} completed between {inviter} and {invitee}.", session.Id, inviter.Name.FullName, invitee.Name.FullName);
                return;
            }

            var (giver, giverMessage, receiverMessage) = failure.Value;
            var receiver = giver.Guid == inviter.Guid ? invitee : inviter;

            SendEnd(session, giver, giverMessage);
            SendEnd(session, receiver, receiverMessage);
        }
    }

    public void Cancel(Player player, int sessionId)
    {
        lock (_lock)
        {
            if (!TryGetSession(player, sessionId, out var session))
                return;

            End(session);

            if (_zoneManager.TryGetPlayer(session.GetPartner(player.Guid)!.Guid, out var partner))
                SendEnd(session, partner, TradeMessageId.Canceled);

            player.SendTunneled(new TradeEndSessionPacket { SessionId = session.Id });
        }
    }

    public void OnLogout(Player player)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(player.Guid, out var session))
                return;

            End(session);

            // Also closes an invite the partner hasn't answered yet.
            if (_zoneManager.TryGetPlayer(session.GetPartner(player.Guid)!.Guid, out var partner))
                SendEnd(session, partner, TradeMessageId.PartnerDisconnected);
        }
    }

    /// <summary>
    /// Swaps both offers in one save. Null when it went through; otherwise whose offer failed and the
    /// message each side gets.
    /// </summary>
    private (Player Giver, TradeMessageId GiverMessage, TradeMessageId ReceiverMessage)? Transfer(TradeSession session, Player inviter, Player invitee)
    {
        var inviterId = GuidHelper.GetPlayerId(inviter.Guid);
        var inviteeId = GuidHelper.GetPlayerId(invitee.Guid);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var characters = dbContext.Characters
            .Include(character => character.Items).ThenInclude(item => item.Profiles)
            .Include(character => character.Items).ThenInclude(item => item.Pets)
            .Where(character => character.Id == inviterId || character.Id == inviteeId)
            .ToDictionary(character => character.Id);

        if (!characters.TryGetValue(inviterId, out var inviterCharacter) || !characters.TryGetValue(inviteeId, out var inviteeCharacter))
            return (inviter, TradeMessageId.YourOfferInvalid, TradeMessageId.PartnerOfferInvalid);

        var sides = new[]
        {
            (Player: inviter, Side: session.Inviter, From: inviterCharacter, To: inviteeCharacter),
            (Player: invitee, Side: session.Invitee, From: inviteeCharacter, To: inviterCharacter)
        };

        // Check both offers before changing anything.
        foreach (var (giver, side, from, to) in sides)
        {
            if (side.Coins > from.Coins)
                return (giver, TradeMessageId.YourOfferInvalid, TradeMessageId.PartnerOfferInvalid);

            foreach (var (itemId, count) in side.Items)
            {
                var dbItem = from.Items.SingleOrDefault(item => item.Id == itemId);

                if (dbItem is null || dbItem.Count < count || dbItem.Profiles.Count > 0 || dbItem.Pets.Count > 0 ||
                    !_resourceManager.ClientItemDefinitions.TryGetValue(dbItem.Definition, out var definition) || definition.NoTrade)
                    return (giver, TradeMessageId.YourOfferInvalid, TradeMessageId.PartnerOfferInvalid);

                var stack = to.Items.FirstOrDefault(item => item.Definition == dbItem.Definition && item.Tint == dbItem.Tint);

                if (definition.MaxStackSize > 0 && (stack?.Count ?? 0) + count > definition.MaxStackSize)
                    return (giver, TradeMessageId.YourOfferExceedsPartnersMaxStack, TradeMessageId.PartnersOfferExceedsYourMaxStack);
            }
        }

        var touched = new Dictionary<ulong, HashSet<int>> { [inviterId] = [], [inviteeId] = [] };

        foreach (var (_, side, from, to) in sides)
        {
            from.Coins -= side.Coins;
            to.Coins += side.Coins;

            foreach (var (itemId, count) in side.Items)
            {
                var dbItem = from.Items.Single(item => item.Id == itemId);
                var stack = to.Items.FirstOrDefault(item => item.Definition == dbItem.Definition && item.Tint == dbItem.Tint);

                if (stack is null)
                {
                    stack = new DbItem
                    {
                        Id = to.Items.Select(item => item.Id).DefaultIfEmpty(0).Max() + 1,
                        Definition = dbItem.Definition,
                        Tint = dbItem.Tint
                    };

                    to.Items.Add(stack);
                }

                stack.Count += count;
                dbItem.Count -= count;

                if (dbItem.Count <= 0)
                {
                    from.Items.Remove(dbItem);
                    dbContext.Items.Remove(dbItem);
                }

                touched[from.Id].Add(itemId);
                touched[to.Id].Add(stack.Id);
            }
        }

        if (dbContext.SaveChanges() <= 0)
            return (inviter, TradeMessageId.YourOfferInvalid, TradeMessageId.PartnerOfferInvalid);

        Sync(inviter, inviterCharacter, touched[inviterId]);
        Sync(invitee, inviteeCharacter, touched[inviteeId]);

        return null;
    }

    /// <summary>
    /// Brings a player's inventory and coins in line with what was just saved.
    /// </summary>
    private void Sync(Player player, DbCharacter character, HashSet<int> itemIds)
    {
        foreach (var itemId in itemIds)
        {
            var dbItem = character.Items.SingleOrDefault(item => item.Id == itemId);
            var clientItem = player.Items.FirstOrDefault(item => item.Id == itemId);

            if (dbItem is null)
            {
                if (clientItem is not null)
                    player.Items.Remove(clientItem);

                player.SendTunneled(new ClientUpdatePacketItemDelete { ItemGuid = itemId });
                player.RemoveItemFromActionBars(itemId);
                continue;
            }

            if (clientItem is not null)
            {
                clientItem.Count = dbItem.Count;
                player.SendTunneled(new ClientUpdatePacketItemUpdate { ItemGuid = clientItem.Id, Count = clientItem.Count });
                continue;
            }

            if (!_resourceManager.ClientItemDefinitions.TryGetValue(dbItem.Definition, out var definition))
                continue;

            clientItem = new ClientItem { Id = dbItem.Id, Definition = dbItem.Definition, Count = dbItem.Count, Tint = dbItem.Tint };
            player.Items.Add(clientItem);

            using var writer = new PacketWriter();
            clientItem.Serialize(writer);
            definition.Serialize(writer);

            player.SendTunneled(new ClientUpdatePacketItemAdd { Payload = writer.Buffer });
        }

        player.Coins = character.Coins;
        player.SendTunneled(new ClientUpdatePacketCoinCount { Coins = player.Coins });
    }

    private bool CanOffer(Player player, ClientItem? clientItem, int count) =>
        clientItem is not null && count <= clientItem.Count && !player.IsItemEquipped(clientItem.Id)
        && _resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var definition) && !definition.NoTrade;

    // Lets both clients know the offers moved under them: their confirmation windows close.
    private static void SendUnlocked(TradeSession session, Player player, Player partner)
    {
        player.SendTunneled(new TradeUpdateLockPacket { SessionId = session.Id, Locked = false });
        partner.SendTunneled(new TradeUpdateLockPacket { SessionId = session.Id, Locked = false });
    }

    // The message goes first, while the client still has the partner's name for it.
    private static void SendEnd(TradeSession session, Player player, TradeMessageId message)
    {
        player.SendTunneled(new TradeMessagePacket { MessageId = message });
        player.SendTunneled(new TradeEndSessionPacket { SessionId = session.Id });
    }

    private void End(TradeSession session)
    {
        _sessions.Remove(session.Inviter.Guid);
        _sessions.Remove(session.Invitee.Guid);
    }

    private bool TryGetSession(Player player, int sessionId, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TradeSession? session) =>
        _sessions.TryGetValue(player.Guid, out session) && session.Id == sessionId;

    private bool TryGetStartedSession(Player player, int sessionId,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TradeSession? session,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Player? partner)
    {
        partner = null;

        if (!TryGetSession(player, sessionId, out session) || !session.Started)
            return false;

        return _zoneManager.TryGetPlayer(session.GetPartner(player.Guid)!.Guid, out partner);
    }
}
