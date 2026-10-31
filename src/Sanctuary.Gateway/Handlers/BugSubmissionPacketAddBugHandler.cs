using System;
using System.Text.RegularExpressions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

/// <summary>
/// The client's bug report window: the report goes to the audit log for referees. The client expects nothing back.
/// </summary>
[PacketHandler]
public static class BugSubmissionPacketAddBugHandler
{
    private static ILogger _logger = null!;
    private static ILogger _auditLogger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BugSubmissionPacketAddBugHandler));
        _auditLogger = loggerFactory.CreateLogger("Audit");
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!BugSubmissionPacketAddBug.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(BugSubmissionPacketAddBug));
            return false;
        }

        _auditLogger.LogInformation("report|bug|Actor: \"{ActorName}\" ({ActorGuid}), Category: \"{Category}/{SubCategory}\", Severity: \"{Severity}\", In: {Zone} {Position}, Report: \"{Description}\"",
            connection.Player.Name.FullName, connection.Player.Guid, Attribute(packet.BugXml, "Category"), Attribute(packet.BugXml, "SubCategory"),
            Attribute(packet.BugXml, "Severity"), connection.Player.Zone.Name, connection.Player.Position, Attribute(packet.BugXml, "BugDescription"));

        return true;
    }

    private static string Attribute(string xml, string name)
    {
        var match = Regex.Match(xml, $"\b{name}=\"([^\"]*)\"");

        return match.Success ? System.Net.WebUtility.HtmlDecode(match.Groups[1].Value) : string.Empty;
    }
}
