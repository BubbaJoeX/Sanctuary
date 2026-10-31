using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Housing;

/// <summary>
/// The Housing Directory: owners submit their houses, others find them (Popular, New, Friends, search, the featured
/// house), visit them and vote one to five stars, once each while the house stays listed.
/// </summary>
public partial class HouseRatingManager : IHouseRatingManager
{
    /// <summary>
    /// A listed house nobody has voted on yet shows this (as live did).
    /// </summary>
    public const float UnratedStars = 3f;

    /// <summary>
    /// The Popular tab weighs each house's average toward <see cref="UnratedStars"/> by this many votes, so one
    /// five-star vote doesn't top the list.
    /// </summary>
    public const int PriorVotes = 5;

    /// <summary>
    /// The most houses a tab or a search shows.
    /// </summary>
    public const int MaxListed = 50;

    public static readonly string ThumbnailsFile = Path.Combine(ResourceManager.BaseDirectory, "HouseThumbnails.json");

    private readonly ILogger _logger;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    private readonly Dictionary<int, string> _thumbnails = [];

    public HouseRatingManager(ILogger<HouseRatingManager> logger, IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;

        LoadThumbnails();
    }

    public void SendCandidateInfo(Player player, string candidateId, ulong ownerGuid)
    {
        var reply = new RatingPacketCandidateInfoReply { PlayerGuid = player.Guid };

        using var db = _dbContextFactory.CreateDbContext();

        if (string.IsNullOrEmpty(candidateId))
        {
            // Every house of the player's: the client only asks this about its own.
            if (ownerGuid == player.Guid)
            {
                var ownerId = GuidHelper.GetPlayerId(ownerGuid);

                foreach (var rating in db.HouseRatings.Include(rating => rating.Votes).Include(rating => rating.House)
                    .Where(rating => rating.House.CharacterId == ownerId).ToList())
                {
                    reply.Candidates.Add(GetInfo(rating, player));
                }
            }
        }
        else if (TryGetHouseId(candidateId, out var houseId))
        {
            var rating = db.HouseRatings.Include(rating => rating.Votes).Include(rating => rating.House)
                .SingleOrDefault(rating => rating.HouseId == houseId);

            // A house that isn't listed still gets an answer, so a panel left from before it was removed clears.
            reply.Candidates.Add(rating is null ? Unlisted(candidateId) : GetInfo(rating, player));
        }

        player.SendTunneled(reply);
    }

    public void Publish(Player player, string candidateId, string description, string keywords)
    {
        if (!TryGetHouseId(candidateId, out var houseId))
            return;

        using var db = _dbContextFactory.CreateDbContext();

        var house = db.Houses.SingleOrDefault(house => house.Id == houseId);

        if (house is null || house.CharacterId != player.CharacterId)
            return;

        // Submitting a listed house again keeps its votes.
        var rating = db.HouseRatings.Include(rating => rating.Votes).SingleOrDefault(rating => rating.HouseId == houseId);

        if (rating is null)
        {
            rating = new DbHouseRating { HouseId = houseId };
            db.HouseRatings.Add(rating);
        }

        rating.Description = Trim(description, 400);
        rating.Keywords = Trim(keywords, 200);
        rating.House = house;

        db.SaveChanges();

        _logger.LogInformation("{player} listed house {house} in the Housing Directory.", player.Name.FullName, houseId);

        player.SendTunneled(new RatingPacketCandidateInfoReply { PlayerGuid = player.Guid, Candidates = [GetInfo(rating, player)] });
    }

    public void Unpublish(Player player, string candidateId)
    {
        if (!TryGetHouseId(candidateId, out var houseId))
            return;

        using var db = _dbContextFactory.CreateDbContext();

        var rating = db.HouseRatings.Include(rating => rating.House).SingleOrDefault(rating => rating.HouseId == houseId);

        if (rating is null || rating.House.CharacterId != player.CharacterId)
            return;

        // Its votes go with it, as the client's warning says.
        db.HouseRatings.Remove(rating);
        db.SaveChanges();

        _logger.LogInformation("{player} removed house {house} from the Housing Directory.", player.Name.FullName, houseId);

        player.SendTunneled(new RatingPacketCandidateInfoReply { PlayerGuid = player.Guid, Candidates = [Unlisted(candidateId)] });
    }

    public void Vote(Player player, string candidateId, float stars)
    {
        if (!TryGetHouseId(candidateId, out var houseId) || float.IsNaN(stars))
            return;

        using var db = _dbContextFactory.CreateDbContext();

        var rating = db.HouseRatings.Include(rating => rating.Votes).Include(rating => rating.House)
            .SingleOrDefault(rating => rating.HouseId == houseId);

        if (rating is null || rating.House.CharacterId == player.CharacterId ||
            rating.Votes.Any(vote => vote.VoterCharacterId == player.CharacterId))
            return;

        rating.Votes.Add(new DbHouseVote
        {
            HouseId = houseId,
            VoterCharacterId = player.CharacterId,
            Stars = Math.Clamp((int)MathF.Round(stars), 1, 5)
        });

        db.SaveChanges();

        // The client's own VoteReply handler is missing; a fresh rating is what updates the panel. Everyone in the
        // house sees the new stars, each with their own vote button.
        if (player.Zone is HousingZone zone && zone.HouseGuid == GuidHelper.GetHouseGuid(houseId))
        {
            foreach (var visitor in zone.Players)
                visitor.SendTunneled(new RatingPacketCandidateInfoReply { PlayerGuid = visitor.Guid, Candidates = [GetInfo(rating, visitor)] });
        }
        else
        {
            player.SendTunneled(new RatingPacketCandidateInfoReply { PlayerGuid = player.Guid, Candidates = [GetInfo(rating, player)] });
        }
    }

    public void SendList(Player player, int requestType)
    {
        var reply = new RatingPacketDataReply { PlayerGuid = player.Guid, RequestType = requestType };

        using var db = _dbContextFactory.CreateDbContext();

        var listed = Listed(db);

        IEnumerable<DbHouseRating> houses = requestType switch
        {
            RatingPacketClientDataRequest.Popular => listed.OrderByDescending(Weighted).ThenByDescending(rating => rating.Votes.Count),
            RatingPacketClientDataRequest.New => listed.OrderByDescending(rating => rating.Published),
            RatingPacketClientDataRequest.Friends => FriendsOf(player, listed),
            _ => listed.OrderBy(_ => Random.Shared.Next())
        };

        reply.Candidates.AddRange(houses.Take(MaxListed).Select(GetCandidate));

        player.SendTunneled(reply);
    }

    public void Search(Player player, string term)
    {
        var reply = new RatingPacketSearchReply { PlayerGuid = player.Guid };
        var search = term.Trim();

        if (search.Length > 0)
        {
            using var db = _dbContextFactory.CreateDbContext();

            reply.Candidates.AddRange(Listed(db)
                .Where(rating => PlainName(rating.House).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    OwnerName(rating.House).Contains(search, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(Weighted)
                .Take(MaxListed)
                .Select(GetCandidate));
        }

        player.SendTunneled(reply);
    }

    public void SendFeatured(Player player)
    {
        using var db = _dbContextFactory.CreateDbContext();

        // The featured house is the top of the Popular tab among houses enough players have voted on.
        var featured = Listed(db)
            .Where(rating => rating.Votes.Count >= 3)
            .OrderByDescending(Weighted)
            .FirstOrDefault();

        player.SendTunneled(new RatingPacketSendFeatured
        {
            PlayerGuid = player.Guid,
            Candidate = featured is null ? new RatingCandidate() : GetCandidate(featured)
        });
    }

    /// <summary>
    /// The listed houses players can enter (a locked one turns visitors away).
    /// </summary>
    private static List<DbHouseRating> Listed(DatabaseContext db) =>
        db.HouseRatings
            .Include(rating => rating.Votes)
            .Include(rating => rating.House).ThenInclude(house => house.Character)
            .Where(rating => !rating.House.IsLocked)
            .AsSplitQuery()
            .ToList();

    private static IEnumerable<DbHouseRating> FriendsOf(Player player, List<DbHouseRating> listed)
    {
        var friends = player.Friends.Select(friend => GuidHelper.GetPlayerId(friend.Guid)).ToHashSet();

        return listed.Where(rating => friends.Contains(rating.House.CharacterId)).OrderByDescending(Weighted);
    }

    private static float Average(DbHouseRating rating) =>
        rating.Votes.Count == 0 ? UnratedStars : (float)rating.Votes.Average(vote => vote.Stars);

    private static float Weighted(DbHouseRating rating) =>
        (UnratedStars * PriorVotes + rating.Votes.Sum(vote => vote.Stars)) / (PriorVotes + rating.Votes.Count);

    private static RatingCandidateInfo GetInfo(DbHouseRating rating, Player player) => new()
    {
        CandidateId = GuidHelper.GetHouseGuid(rating.HouseId).ToString(),
        Description = rating.Description ?? string.Empty,
        Keywords = rating.Keywords ?? string.Empty,
        Rating = Average(rating),
        NumVotes = rating.Votes.Count,
        HasRating = true,
        CanVote = rating.House.CharacterId != player.CharacterId && rating.Votes.All(vote => vote.VoterCharacterId != player.CharacterId)
    };

    private static RatingCandidateInfo Unlisted(string candidateId) => new() { CandidateId = candidateId, Rating = UnratedStars };

    private RatingCandidate GetCandidate(DbHouseRating rating) => new()
    {
        CandidateId = GuidHelper.GetHouseGuid(rating.HouseId).ToString(),
        OwnerName = OwnerName(rating.House),
        Name = HouseName(rating.House),
        OwnerGuid = GuidHelper.GetPlayerGuid(rating.House.CharacterId),
        Snapshot = _thumbnails.GetValueOrDefault(rating.House.Definition, string.Empty),
        Description = rating.Description ?? string.Empty,
        Keywords = rating.Keywords ?? string.Empty,
        Rating = Average(rating),
        NumVotes = rating.Votes.Count
    };

    /// <summary>
    /// The name the owner gave the house (the client fills in "{n1}" style tokens itself), or "Owner's House".
    /// </summary>
    private static string HouseName(DbHouse house) =>
        !string.IsNullOrWhiteSpace(house.Name) ? house.Name
        : OwnerName(house) is { Length: > 0 } owner ? $"{owner}'s House"
        : "House";

    private static string PlainName(DbHouse house) => TokenRegex().Replace(HouseName(house), string.Empty);

    private static string OwnerName(DbHouse house) => house.Character?.FullName ?? house.Character?.FirstName ?? string.Empty;

    private static bool TryGetHouseId(string candidateId, out ulong houseId)
    {
        houseId = 0;

        if (!ulong.TryParse(candidateId, out var houseGuid) || !GuidHelper.IsHouseGuid(houseGuid))
            return false;

        houseId = GuidHelper.GetHouseId(houseGuid);
        return houseId != 0;
    }

    private static string Trim(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

    private void LoadThumbnails()
    {
        try
        {
            if (!File.Exists(ThumbnailsFile))
                return;

            foreach (var entry in JsonDocument.Parse(File.ReadAllText(ThumbnailsFile)).RootElement.EnumerateArray())
                _thumbnails[entry.GetProperty("HouseDefinitionId").GetInt32()] = entry.GetProperty("Thumbnail").GetString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read \"{file}\".", ThumbnailsFile);
        }
    }

    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex TokenRegex();
}
