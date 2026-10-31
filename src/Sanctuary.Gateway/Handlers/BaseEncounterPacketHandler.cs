using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game.Battles;
using Sanctuary.Game.VehicleGames;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

/// <summary>
/// Battle requests, for <see cref="IBattleManager"/>: Play and Cancel on the start panel, leaving, the knocked out
/// window and group battle invitations. The 3D games (<see cref="IVehicleGameManager"/>) get first look at Play,
/// Cancel, leaving and invitation answers. Anything else is logged in full.
/// </summary>
[PacketHandler]
public static class BaseEncounterPacketHandler
{
    private static ILogger _logger = null!;
    private static IBattleManager _battleManager = null!;
    private static IVehicleGameManager _vehicleGameManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseEncounterPacketHandler));

        _battleManager = serviceProvider.GetRequiredService<IBattleManager>();
        _vehicleGameManager = serviceProvider.GetRequiredService<IVehicleGameManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        // The tunnel handler has read the opcode already; each request is read whole, from it.
        reader.Reset();

        var data = reader.Span;
        var player = connection.Player;

        if (!reader.TryRead(out short _) || !reader.TryRead(out short subOpCode))
        {
            _logger.LogError("Failed to read the encounter sub-op. ( Data: {data} )", Convert.ToHexString(data));
            return false;
        }

        switch (subOpCode)
        {
            case EncounterParticipantRequestEntrancePacket.OpCode when EncounterParticipantRequestEntrancePacket.TryDeserialize(data, out var entrance):
                if (!_vehicleGameManager.RequestEntrance(player, entrance.EncounterId))
                    _battleManager.RequestEntrance(player, entrance.EncounterId);
                return true;

            case EncounterParticipantCancelPendingEncounterPacket.OpCode when EncounterParticipantCancelPendingEncounterPacket.TryDeserialize(data, out _):
                if (!_vehicleGameManager.CancelPending(player))
                    _battleManager.CancelPending(player);
                return true;

            case EncounterParticipantRequestExitPacket.OpCode when EncounterParticipantRequestExitPacket.TryDeserialize(data, out _):
                if (!_vehicleGameManager.RequestExit(player))
                    _battleManager.RequestExit(player);
                return true;

            case EncounterParticipantResumePacket.OpCode when EncounterParticipantResumePacket.TryDeserialize(data, out var resume):
                _battleManager.Resume(player, resume.Paid);
                return true;

            // The answer names no encounter: whichever asked the player along takes it.
            case EncounterInvitationResponsePacket.OpCode when EncounterInvitationResponsePacket.TryDeserialize(data, out var answer):
                if (!_vehicleGameManager.AnswerInvitation(player, answer.Accept))
                    _battleManager.AnswerInvitation(player, answer.Accept);
                return true;
        }

        _logger.LogInformation("{player} sent an encounter packet. ( Data: {data} )", player.Name.FullName, Convert.ToHexString(data));

        return true;
    }
}
