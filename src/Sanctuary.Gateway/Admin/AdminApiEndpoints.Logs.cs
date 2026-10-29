using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Sanctuary.Gateway.Admin;

/// <summary>
/// The gateway's own log files (NLog.config writes them to Logs beside it, one a day): chat and the audit log for
/// every referee, the info and error logs for admins.
/// </summary>
public static partial class AdminApiEndpoints
{
    private const int DefaultLogEntries = 300;
    private const int MaxLogEntries = 2000;

    private static readonly string LogsDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");

    private static readonly string ProcessName = Process.GetCurrentProcess().ProcessName;

    private sealed record LogKind(string Key, string Name, string Prefix, bool AdminOnly);

    private static readonly LogKind[] LogKinds =
    [
        new("chat", "Chat", "Chat-", false),
        new("audit", "Audit", "Audit-", false),
        new("errors", "Errors and warnings", $"{ProcessName}-Error-", true),
        new("info", "Info", $"{ProcessName}-Info-", true)
    ];

    // An entry's first line: "2026-10-29 12:49:18.6111|INFO|Chat|...". Others (stack traces) belong to the one before.
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+\|")]
    private static partial Regex LogEntryStart();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex LogDate();

    private static void MapLogEndpoints(RouteGroupBuilder referee)
    {
        referee.MapGet("/logs", LogsHandler);
        referee.MapGet("/logs/{kind}", LogHandler);
    }

    private static LogKindModel[] LogsHandler(HttpContext context)
    {
        var referee = GetReferee(context);

        return LogKinds
            .Where(kind => referee.IsAdmin || !kind.AdminOnly)
            .Select(kind => new LogKindModel(kind.Key, kind.Name, LogDates(kind)))
            .ToArray();
    }

    /// <summary>
    /// A day's entries (the newest day's by default), newest first, those whose level is <paramref name="level"/> and
    /// whose logger or message has <paramref name="search"/> in it.
    /// </summary>
    private static IResult LogHandler(HttpContext context, string kind, string? date, string? search, string? level, int? limit)
    {
        var referee = GetReferee(context);

        if (LogKinds.FirstOrDefault(x => x.Key.Equals(kind, StringComparison.OrdinalIgnoreCase)) is not { } logKind)
            return Results.NotFound($"There's no {kind} log.");

        if (logKind.AdminOnly && !referee.IsAdmin)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        date ??= LogDates(logKind).FirstOrDefault() ?? DateTime.Now.ToString("yyyy-MM-dd");

        if (!LogDate().IsMatch(date))
            return Results.BadRequest("A date is yyyy-MM-dd.");

        var count = Math.Clamp(limit ?? DefaultLogEntries, 1, MaxLogEntries);
        var path = Path.Combine(LogsDirectory, $"{logKind.Prefix}{date}.log");

        if (!File.Exists(path))
            return Results.Ok(new LogPageModel(date, [], false));

        var matches = ReadLog(path)
            .Where(entry => string.IsNullOrEmpty(level) || entry.Level.Equals(level, StringComparison.OrdinalIgnoreCase))
            .Where(entry => string.IsNullOrWhiteSpace(search)
                || entry.Message.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.Logger.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var entries = matches.TakeLast(count).Reverse().ToArray();

        return Results.Ok(new LogPageModel(date, entries, matches.Count > count));
    }

    private static string[] LogDates(LogKind kind)
    {
        if (!Directory.Exists(LogsDirectory))
            return [];

        return Directory.EnumerateFiles(LogsDirectory, $"{kind.Prefix}*.log")
            .Select(path => Path.GetFileNameWithoutExtension(path)[kind.Prefix.Length..])
            .Where(date => LogDate().IsMatch(date))
            .OrderDescending()
            .ToArray();
    }

    private static List<LogEntryModel> ReadLog(string path)
    {
        var entries = new List<LogEntryModel>();

        // NLog keeps today's file open for writing.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            if (LogEntryStart().IsMatch(line))
            {
                var parts = line.Split('|', 4);
                entries.Add(new LogEntryModel(parts[0], parts[1], parts[2], parts.Length > 3 ? parts[3] : string.Empty));
            }
            else if (entries.Count > 0 && line.Length > 0)
            {
                var last = entries[^1];
                entries[^1] = last with { Message = $"{last.Message}\n{line}" };
            }
        }

        return entries;
    }
}
