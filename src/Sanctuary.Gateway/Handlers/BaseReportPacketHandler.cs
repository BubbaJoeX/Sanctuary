using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

/// <summary>
/// Reports of players and houses go to the audit log, which referees read in the AdminPanel. The client expects
/// nothing back.
/// </summary>
[PacketHandler]
public static class BaseReportPacketHandler
{
    private static ILogger _logger = null!;
    private static ILogger _auditLogger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseReportPacketHandler));
        _auditLogger = loggerFactory.CreateLogger("Audit");
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        if (!reader.TryRead(out short opCode))
        {
            _logger.LogError("Failed to read opcode from packet. ( Data: {data} )", Convert.ToHexString(reader.Span));
            return false;
        }

        return opCode switch
        {
            ReportPacketReportPlayer.OpCode => ReportPlayer(connection, reader.Span),
            ReportPacketReportHouse.OpCode => ReportHouse(connection, reader.Span),
            _ => false
        };
    }

    private static bool ReportPlayer(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ReportPacketReportPlayer.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ReportPacketReportPlayer));
            return false;
        }

        var lines = string.Join(" / ", packet.Lines.Select(line =>
            $"[{DateTimeOffset.FromUnixTimeSeconds((long)line.UnixTime):u} channel {line.Channel}] {line.Message}"));

        _auditLogger.LogInformation("report|player|Actor: \"{ActorName}\" ({ActorGuid}), Target: \"{Reported}\", Chat (from the reporter's client): {Lines}",
            connection.Player.Name.FullName, connection.Player.Guid, packet.ReportedName, lines);

        return true;
    }

    private static bool ReportHouse(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ReportPacketReportHouse.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ReportPacketReportHouse));
            return false;
        }

        _auditLogger.LogInformation("report|house|Actor: \"{ActorName}\" ({ActorGuid}), Target: house {HouseId} \"{HouseName}\" of \"{OwnerName}\" ({OwnerGuid}) in {Zone}",
            connection.Player.Name.FullName, connection.Player.Guid, GuidHelper.GetHouseId(packet.HouseGuid), packet.HouseName,
            packet.OwnerName, packet.OwnerGuid, connection.Player.Zone.Name);

        return true;
    }
}
