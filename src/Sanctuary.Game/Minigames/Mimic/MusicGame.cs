using System.Collections.Generic;
using System.Linq;

namespace Sanctuary.Game.Minigames.Mimic;

/// <summary>
/// Feeling the Beat! (game_music.swf): conduct an orchestra to the beat of a song. The movie has its five songs
/// built in (MusicConducting.Songs); a game's row in Minigames/Music/MimicMiniGamePhaseData.txt only picks which
/// one it plays, and the movie plays that one round and reports. Nothing is reported while the song plays, so the
/// server takes a win only once the song could have been heard through.
/// </summary>
[FlashMinigame("Music")]
public sealed class MusicGame : MimicGame
{
    /// <summary>
    /// The songs (300 + the song: 1 Very Easy to 5 Very Hard).
    /// </summary>
    private const int FirstSong = 301;
    private const int LastSong = 305;

    /// <summary>
    /// The share of a song's length the server waits for before it takes a win.
    /// </summary>
    private const double MinimumShareOfSong = 0.9;

    protected override int DataColumns => 4;

    protected override bool PlayedEveryRound => true;

    /// <summary>
    /// The song plays to its end before the movie can call it a win; a player can be booed off sooner.
    /// </summary>
    protected override long MinimumWinMilliseconds =>
        (long)(Phases.Sum(x => x.Duration) * 1000 * MinimumShareOfSong);

    protected override IEnumerable<string> CheckRounds(IReadOnlyList<MimicRow> rounds, IReadOnlyList<MimicRow> custom)
    {
        if (rounds.Count > 1)
            yield return $"Game {RecipeId}: the movie plays one song, not {rounds.Count}.";

        foreach (var round in rounds)
        {
            if (round.StageType is < FirstSong or > LastSong)
                yield return $"Row {round}: song {round.StageType} isn't one of the movie's ({FirstSong}-{LastSong}).";

            if (round.Duration <= 0)
                yield return $"Row {round}: the song's length in seconds is what a win is timed against.";
        }
    }
}
