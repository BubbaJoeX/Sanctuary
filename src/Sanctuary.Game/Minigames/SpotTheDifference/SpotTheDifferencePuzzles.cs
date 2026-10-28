using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Sanctuary.Game.Minigames.SpotTheDifference;

/// <summary>
/// Spot the Difference's puzzles (Resources/Minigames/SpotTheDifference/Puzzles.json): the movie's pictures and
/// where their differences are, and how each difficulty plays them. The movie's own server read these from data
/// files (HiddenObjectMiniGameData, ...LevelData and ...ItemData) that never shipped with the client.
/// </summary>
public sealed class SpotTheDifferencePuzzles
{
    public const string FileName = "Puzzles.json";

    public List<SpotTheDifferenceBoard> Boards { get; set; } = [];

    public List<SpotTheDifferenceLevel> Levels { get; set; } = [];

    public static SpotTheDifferencePuzzles Load(string path)
    {
        using var stream = File.OpenRead(path);

        return JsonSerializer.Deserialize<SpotTheDifferencePuzzles>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        }) ?? new SpotTheDifferencePuzzles();
    }
}

/// <summary>
/// A picture: the frame of the movie's level view that shows it, and its differences.
/// </summary>
public sealed class SpotTheDifferenceBoard
{
    public int Frame { get; set; }

    public string Comment { get; set; } = string.Empty;

    public List<SpotTheDifferenceSpot> Differences { get; set; } = [];
}

/// <summary>
/// A difference: the clip in the picture that marks it, the box a click must land in (the level view's pixels,
/// as the movie's HiddenLevel.DebugDumpItems measures them), and the string the objective list names it by.
/// </summary>
public sealed class SpotTheDifferenceSpot
{
    public string Clip { get; set; } = string.Empty;

    public int NameId { get; set; }

    public int Left { get; set; }

    public int Top { get; set; }

    public int Right { get; set; }

    public int Bottom { get; set; }

    /// <summary>
    /// The movie's HiddenItem.Contains: edges count.
    /// </summary>
    public bool Contains(int x, int y) => x >= Left && x <= Right && y >= Top && y <= Bottom;
}

/// <summary>
/// How a difficulty plays (the catalog entry's Level picks one): the rounds, each a different picture, and the
/// clock, the hints and the misclick penalty.
/// </summary>
public sealed class SpotTheDifferenceLevel
{
    public int Level { get; set; }

    public string Comment { get; set; } = string.Empty;

    public int Rounds { get; set; } = 5;

    /// <summary>
    /// The frames to play, in order; empty plays <see cref="Rounds"/> pictures drawn at random.
    /// </summary>
    public List<int> Frames { get; set; } = [];

    public int RoundSeconds { get; set; } = 60;

    /// <summary>
    /// The hint buttons shown, and how many of them can be used.
    /// </summary>
    public int HintsMax { get; set; } = 5;

    public int HintsAtStart { get; set; } = 3;

    /// <summary>
    /// Hints given back at the start of each round after the first.
    /// </summary>
    public int HintsPerRound { get; set; }

    /// <summary>
    /// Misses in a row that cost <see cref="TimePenalty"/> seconds (the movie's wrong-click penalty).
    /// </summary>
    public int WrongClicks { get; set; } = 5;

    public int TimePenalty { get; set; } = 10;

    public int PointsPerDifference { get; set; } = 100;

    /// <summary>
    /// Points for each second left when a round is cleared.
    /// </summary>
    public int PointsPerSecondLeft { get; set; } = 10;
}
