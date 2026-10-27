using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Game.Helpers;

/// <summary>
/// A named spot referees jump to with !gm go (event stages, meeting points), shared by every referee.
/// </summary>
public sealed class RefereeBookmark
{
    public required string Name { get; init; }
    public int ZoneDefinitionId { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public float RotationX { get; init; }
    public float RotationY { get; init; }
    public float RotationZ { get; init; }
    public float RotationW { get; init; } = 1f;
    public string? CreatedBy { get; init; }
}

/// <summary>
/// Referee bookmarks, kept in a JSON file in the server's working directory so they survive restarts.
/// </summary>
public sealed class RefereeBookmarks
{
    public const int MaxNameLength = 32;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger _logger;
    private readonly string _filePath;
    private readonly object _lock = new();
    private Dictionary<string, RefereeBookmark>? _bookmarks;

    public RefereeBookmarks(ILogger logger, string filePath = "RefereeBookmarks.json")
    {
        _logger = logger;
        _filePath = filePath;
    }

    public IReadOnlyList<RefereeBookmark> GetAll()
    {
        lock (_lock)
            return [.. Load().Values.OrderBy(x => x.Name, StringComparer.Ordinal)];
    }

    public bool TryGet(string name, out RefereeBookmark bookmark)
    {
        lock (_lock)
            return Load().TryGetValue(name, out bookmark!);
    }

    public bool Set(RefereeBookmark bookmark)
    {
        lock (_lock)
        {
            var bookmarks = Load();
            bookmarks.TryGetValue(bookmark.Name, out var previous);
            bookmarks[bookmark.Name] = bookmark;

            if (Save(bookmarks))
                return true;

            if (previous is null)
                bookmarks.Remove(bookmark.Name);
            else
                bookmarks[bookmark.Name] = previous;

            return false;
        }
    }

    public bool Remove(string name)
    {
        lock (_lock)
        {
            var bookmarks = Load();

            if (!bookmarks.Remove(name, out var removed))
                return false;

            if (Save(bookmarks))
                return true;

            bookmarks[name] = removed;
            return false;
        }
    }

    /// <summary>
    /// Bookmark names are one lowercase word: spaces become dashes, and ':' is reserved by the panel's lines.
    /// </summary>
    public static string? NormalizeName(IEnumerable<string> words)
    {
        var name = string.Join('-', words).Trim().ToLowerInvariant();

        return name.Length is > 0 and <= MaxNameLength && !name.Contains(':') ? name : null;
    }

    private Dictionary<string, RefereeBookmark> Load()
    {
        if (_bookmarks is not null)
            return _bookmarks;

        _bookmarks = [];

        if (!File.Exists(_filePath))
            return _bookmarks;

        try
        {
            foreach (var bookmark in JsonSerializer.Deserialize<List<RefereeBookmark>>(File.ReadAllText(_filePath), JsonOptions) ?? [])
                _bookmarks[bookmark.Name] = bookmark;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read referee bookmarks from \"{file}\".", _filePath);
        }

        return _bookmarks;
    }

    private bool Save(Dictionary<string, RefereeBookmark> bookmarks)
    {
        try
        {
            var json = JsonSerializer.Serialize(bookmarks.Values.OrderBy(x => x.Name, StringComparer.Ordinal), JsonOptions);
            var temporaryPath = _filePath + ".tmp";
            File.WriteAllText(temporaryPath, json + Environment.NewLine);
            File.Move(temporaryPath, _filePath, true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save referee bookmarks to \"{file}\".", _filePath);
            return false;
        }
    }
}
