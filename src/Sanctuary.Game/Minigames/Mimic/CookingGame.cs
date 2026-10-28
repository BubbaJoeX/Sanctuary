using System.Collections.Generic;
using System.Linq;

namespace Sanctuary.Game.Minigames.Mimic;

/// <summary>
/// Cooking (game_cooking.swf, the Chef's recipes). Each recipe is a run of rounds, one per row of
/// Minigames/Cooking/MimicMiniGamePhaseData.txt: the ingredients are prepared (sliced, cut, grated,
/// tenderized, smashed, chopped, sawn, cracked, measured), added to the dish and the dish cooked or stirred.
/// The movie sends FRServer_RoundTransitionPause true as each round's score screen comes up; a failed round
/// starts over instead, so a win has one for every round.
/// </summary>
[FlashMinigame("Cooking")]
public sealed class CookingGame : MimicGame
{
    /// <summary>
    /// The movie's rounds (Cooking.StageType) that it has a frame for; its pour, grind, stuff and final rounds
    /// never got one.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, string> Stages = new Dictionary<int, string>
    {
        [1] = "tenderize",
        [2] = "chop",
        [3] = "smashFruit",
        [4] = "crack",
        [6] = "measure",
        [7] = "stir",
        [8] = "saw",
        [9] = "grate",
        [10] = "slice",
        [11] = "cook",
        [12] = "cut",
        [13] = "setHeat",
        [17] = "addIngredients"
    };

    /// <summary>
    /// Rounds whose ingredient goes on the list the add round asks for.
    /// </summary>
    private static readonly HashSet<int> PreparingStages = [1, 2, 3, 9, 10, 12];

    /// <summary>
    /// What each round has art for (Cooking.CookingIngredient ids): the frames of its clip in the movie.
    /// </summary>
    private static readonly Dictionary<int, int[]> StageIngredients = new()
    {
        [1] = [8, 9, 10, 11],                       // Mystery, Bear, Bat and Spider Meat
        [2] = [24, 25, 26, 27, 28],                 // the leaves
        [3] = [34, 35, 36, 37, 38, 39, 40, 41, 56], // the fruits and Tomato
        [4] = [23],                                 // Egg
        [6] = [19, 20, 21, 22, 23, 48, 49, 50, 51, 52, 53, 54], // Slugmud, Rootbeer, ice cream, Egg, the broths, Spaghetti sauce
        [7] = [46, 47, 54],                         // Meat Stew, Fish Soup, Spaghetti
        [8] = [8, 9, 10, 11],                       // the meats
        [9] = [31, 32, 33],                         // Chugaroot, Chocolate, Honeytwig
        [10] = [12, 13, 14, 15, 16, 17, 18, 57],    // the vegetables and Onion
        [11] = [42, 43, 54],                        // Meat Saute, Fish Saute, Spaghetti
        [12] = [0, 1, 2, 3, 4, 5, 6, 7],            // the fish
        [13] = [21, 22],                            // Vanilla and Chocolate Ice Cream
        [17] = [42, 43, 44, 45, 46, 47, 54]         // Meat Saute, Fish Saute, Sundae, Omelet, Meat Stew, Fish Soup, Spaghetti
    };

    private const int MeasureStage = 6;
    private const int StirStage = 7;
    private const int AddStage = 17;
    private const int MeatStew = 46;
    private const int FishSoup = 47;

    /// <summary>
    /// The most rounds the movie's round list shows (CookingData.MAX_ROUNDS), and the most ingredients the add
    /// round lists.
    /// </summary>
    private const int MaxRounds = 8;
    private const int MaxIngredients = 5;

    protected override int DataColumns => 11;

    protected override bool PlayedEveryRound => RoundsCompleted >= Phases.Count;

    protected override IEnumerable<string> CheckRounds(IReadOnlyList<MimicRow> rounds, IReadOnlyList<MimicRow> custom)
    {
        if (rounds.Count > MaxRounds)
            yield return $"Recipe {RecipeId} has {rounds.Count} rounds; the movie lists {MaxRounds}.";

        var prepared = 0;
        var measured = false;

        foreach (var round in rounds)
        {
            if (!Stages.TryGetValue(round.StageType, out var stage))
            {
                yield return $"Row {round}: the movie has no round {round.StageType}.";
                continue;
            }

            if (!StageIngredients[round.StageType].Contains(round.Data(1)))
                yield return $"Row {round}: the {stage} round has no art for ingredient {round.Data(1)}.";

            if (PreparingStages.Contains(round.StageType))
                prepared++;

            if (round.StageType == MeasureStage)
                measured = true;

            if (round.StageType == AddStage)
            {
                // The add round asks for what was prepared before it; with nothing on its list it has nothing to lay out.
                if (prepared == 0)
                    yield return $"Row {round}: nothing is prepared before the ingredients are added.";
                else if (prepared > MaxIngredients)
                    yield return $"Row {round}: {prepared} ingredients to add; the movie lists {MaxIngredients}.";
            }

            // A stew or a soup takes the broth the measure round poured.
            if ((round.StageType is AddStage or StirStage) && (round.Data(1) is MeatStew or FishSoup) && !measured)
                yield return $"Row {round}: the broth must be measured before the {stage} round.";
        }
    }
}
