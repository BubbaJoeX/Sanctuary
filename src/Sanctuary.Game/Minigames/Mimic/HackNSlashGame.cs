using System.Collections.Generic;
using System.Linq;

namespace Sanctuary.Game.Minigames.Mimic;

/// <summary>
/// Hack 'n' Slash (game_sword.swf): follow the moves of sword techniques on a training dummy. Each row of
/// Minigames/HackNSlash/MimicMiniGamePhaseData.txt is a round, the movie picking its technique from the row's
/// four; MimicSwordData.txt (sent as OnCustomDataMsg) gives the player's lives and the dummy's armor. The movie
/// reports each round it scores (FRServer_CompleteLevel) and, after the last, FRServer_StopLevel with whether the
/// dummy took enough damage; only then does it report a win.
/// </summary>
[FlashMinigame("HackNSlash")]
public sealed class HackNSlashGame : MimicGame
{
    public const string SwordDataFileName = "MimicSwordData.txt";

    /// <summary>
    /// The techniques the movie can pick (SwordTesting.SwordGameServer: 1 Cut Thrust to 22 Dismemberer).
    /// </summary>
    private const int LastTechnique = 22;

    protected override string CustomDataFileName => SwordDataFileName;

    protected override int DataColumns => 5;

    protected override bool PlayedEveryRound => StoppedWinner && StoppedLevel >= Phases.Count;

    protected override IEnumerable<string> CheckRounds(IReadOnlyList<MimicRow> rounds, IReadOnlyList<MimicRow> custom)
    {
        foreach (var round in rounds)
        {
            var techniques = Enumerable.Range(2, 4).Select(round.Data).ToList();

            if (techniques.Any(x => x is < 0 or > LastTechnique))
                yield return $"Row {round}: techniques are 1 to {LastTechnique}, or 0 for none.";
        }

        // The movie reads the game's first row: lives, then the armor and body pieces' hits.
        if (custom.Count == 0)
            yield return $"{SwordDataFileName} has no dummy for game {RecipeId}.";
        else if (custom[0].Count < 9 || custom[0].Int(2) <= 0)
            yield return $"Row {custom[0]}: the dummy needs the player's lives and its six pieces' hits.";
    }
}
