using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game.Housing;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

/// <summary>
/// The Housing Directory (BaseRatingPacket 174): listing houses, voting, the browser's tabs, search and the featured
/// house. Requests for any rating system but houses are ignored.
/// </summary>
[PacketHandler]
public static class BaseRatingPacketHandler
{
    private static ILogger _logger = null!;
    private static IHouseRatingManager _houseRatingManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseRatingPacketHandler));

        _houseRatingManager = serviceProvider.GetRequiredService<IHouseRatingManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        if (!reader.TryRead(out byte opCode))
        {
            _logger.LogError("Failed to read opcode from packet. ( Data: {data} )", Convert.ToHexString(reader.Span));
            return false;
        }

        var player = connection.Player;

        switch (opCode)
        {
            case RatingPacketCandidateInfoRequest.OpCode when RatingPacketCandidateInfoRequest.TryDeserialize(reader.Span, out var request):
                if (IsHousing(request.System))
                    _houseRatingManager.SendCandidateInfo(player, request.CandidateId, request.OwnerGuid);
                return true;

            case RatingPacketAddCandidate.OpCode when RatingPacketAddCandidate.TryDeserialize(reader.Span, out var request):
                if (IsHousing(request.System))
                    _houseRatingManager.Publish(player, request.CandidateId, request.Description, request.Keywords);
                return true;

            case RatingPacketRemoveCandidate.OpCode when RatingPacketRemoveCandidate.TryDeserialize(reader.Span, out var request):
                if (IsHousing(request.System))
                    _houseRatingManager.Unpublish(player, request.CandidateId);
                return true;

            case RatingPacketAddVote.OpCode when RatingPacketAddVote.TryDeserialize(reader.Span, out var request):
                if (IsHousing(request.System))
                    _houseRatingManager.Vote(player, request.CandidateId, request.Vote);
                return true;

            case RatingPacketClientDataRequest.OpCode when RatingPacketClientDataRequest.TryDeserialize(reader.Span, out var request):
                if (IsHousing(request.System))
                    _houseRatingManager.SendList(player, request.RequestType);
                return true;

            case RatingPacketSearchRequest.OpCode when RatingPacketSearchRequest.TryDeserialize(reader.Span, out var request):
                if (IsHousing(request.System))
                    _houseRatingManager.Search(player, request.SearchTerm);
                return true;

            case RatingPacketRequestFeatured.OpCode when RatingPacketRequestFeatured.TryDeserialize(reader.Span, out var request):
                if (IsHousing(request.System))
                    _houseRatingManager.SendFeatured(player);
                return true;

            default:
                _logger.LogWarning("Unhandled or malformed rating packet {sub}. ( Data: {data} )", opCode, Convert.ToHexString(reader.Span));
                return false;
        }
    }

    private static bool IsHousing(string system) => system == BaseRatingPacket.HousingSystem;
}
