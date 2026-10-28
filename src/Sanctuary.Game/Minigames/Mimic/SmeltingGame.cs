using System.Collections.Generic;

namespace Sanctuary.Game.Minigames.Mimic;

/// <summary>
/// Smelting (game_smelting.swf, the Blacksmith's bars): break down the ore, fill the furnace with its powder, heat
/// the furnace and pour the metal into the mold, one row each of Minigames/Smelting/MimicMiniGamePhaseData.txt.
/// The ore round names the ore, which every later round is colored and measured for. Like cooking, the movie
/// sends FRServer_RoundTransitionPause true as each round ends and starts a failed round over.
/// </summary>
[FlashMinigame("Smelting")]
public sealed class SmeltingGame : MimicGame
{
    /// <summary>
    /// The movie's rounds (100 + Smelting.SmeltingData.STAGE_*).
    /// </summary>
    public const int BreakDownOre = 101;
    public const int FillFurnace = 102;
    public const int HeatFurnace = 103;
    public const int MoldMetal = 104;

    /// <summary>
    /// The ores (Smelting.SmeltingGameServer): 1 Copper, 2 Tin, 3 Iron, 4 Silver, 5 Gold.
    /// </summary>
    private const int FirstOre = 1;
    private const int LastOre = 5;

    protected override int DataColumns => 11;

    protected override bool PlayedEveryRound => RoundsCompleted >= Phases.Count;

    protected override IEnumerable<string> CheckRounds(IReadOnlyList<MimicRow> rounds, IReadOnlyList<MimicRow> custom)
    {
        if (rounds.Count > 0 && rounds[0].StageType != BreakDownOre)
            yield return $"Bar {RecipeId}: the ore must be broken down first; the later rounds take its color from it.";

        foreach (var round in rounds)
        {
            switch (round.StageType)
            {
                case BreakDownOre:
                    if (round.Data(1) is < FirstOre or > LastOre)
                        yield return $"Row {round}: ore {round.Data(1)} isn't one the movie knows.";
                    break;

                case FillFurnace:
                    // The movie keeps its default line for anything outside 1-100 %.
                    if (round.Data(1) is < 1 or > 100)
                        yield return $"Row {round}: the powder line is a percentage of the furnace.";
                    break;

                case HeatFurnace:
                    if (round.Data(1) <= 0)
                        yield return $"Row {round}: the furnace needs a heat to reach.";
                    break;

                case MoldMetal:
                    break;

                default:
                    yield return $"Row {round}: the movie has no round {round.StageType}.";
                    break;
            }
        }
    }
}
