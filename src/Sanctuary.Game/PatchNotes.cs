using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Sanctuary.Game;

/// <summary>
/// The welcome screen's patch notes: the latest commits of the repository the server runs from, newest first.
/// Read with git when first asked for and every few minutes after; empty when there's no git or repository.
/// </summary>
public static class PatchNotes
{
    public const int MaxNotes = 30;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(5);

    private static readonly object Lock = new();
    private static IReadOnlyList<(string Date, string Subject)> _notes = [];
    private static DateTime _readAt = DateTime.MinValue;

    public static IReadOnlyList<(string Date, string Subject)> Current
    {
        get
        {
            lock (Lock)
            {
                if (DateTime.UtcNow - _readAt > RefreshInterval)
                {
                    _notes = Read();
                    _readAt = DateTime.UtcNow;
                }

                return _notes;
            }
        }
    }

    private static IReadOnlyList<(string Date, string Subject)> Read()
    {
        var repository = FindRepository(AppContext.BaseDirectory);

        if (repository is null)
            return [];

        try
        {
            var startInfo = new ProcessStartInfo("git")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in new[] { "-C", repository, "log", $"-n{MaxNotes}", "--no-merges", "--date=short", "--pretty=format:%ad|%s" })
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);

            if (process is null)
                return [];

            var output = process.StandardOutput.ReadToEnd();

            if (!process.WaitForExit(GitTimeout))
            {
                process.Kill();
                return [];
            }

            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.Split('|', 2))
                .Where(parts => parts.Length == 2)
                .Select(parts => (parts[0], parts[1]))
                .ToList();
        }
        catch (Exception)
        {
            // No git on this machine.
            return [];
        }
    }

    private static string? FindRepository(string directory)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            // A worktree has a .git file instead of a directory.
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git")))
                return current.FullName;
        }

        return null;
    }
}
