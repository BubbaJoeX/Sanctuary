using System;
using System.Collections.Generic;
using System.Linq;

using Sanctuary.Game.Minigames.Soe;

namespace Sanctuary.Game.Minigames.SpotTheDifference;

/// <summary>
/// One round of Spot the Difference: a picture, its timer and the differences found so far (the movie's
/// GameLevel, on its server). The round's score is the number found, against a goal of all of them.
/// </summary>
public sealed class SpotTheDifferenceRound : SoeLevel
{
    private readonly bool[] _found;
    private readonly bool[] _hinted;

    public SpotTheDifferenceRound(SoeGameServer game, SpotTheDifferenceBoard board, SpotTheDifferenceLevel level) : base(game)
    {
        Board = board;
        _found = new bool[board.Differences.Count];
        _hinted = new bool[board.Differences.Count];

        Goal = board.Differences.Count;
        SetEndTime(level.RoundSeconds);
    }

    public SpotTheDifferenceBoard Board { get; }

    public IReadOnlyList<SpotTheDifferenceSpot> Spots => Board.Differences;

    /// <summary>
    /// Misses since the last find or penalty.
    /// </summary>
    public int WrongCount { get; set; }

    public int FoundCount => _found.Count(found => found);

    public bool IsCleared => FoundCount == _found.Length;

    public bool IsFound(int index) => _found[index];

    public override void OnStartLevel()
    {
        Array.Clear(_found);
        Array.Clear(_hinted);
        WrongCount = 0;
    }

    /// <summary>
    /// The difference not yet found under a click, or -1.
    /// </summary>
    public int FindAt(int x, int y)
    {
        for (var i = 0; i < _found.Length; i++)
        {
            if (!_found[i] && Spots[i].Contains(x, y))
                return i;
        }

        return -1;
    }

    public void MarkFound(int index)
    {
        _found[index] = true;
    }

    /// <summary>
    /// A difference to point out: one not found or pointed out yet, else any not found; -1 when all are found.
    /// </summary>
    public int PickHint(Random random)
    {
        var candidates = Enumerable.Range(0, _found.Length).Where(i => !_found[i] && !_hinted[i]).ToList();

        if (candidates.Count == 0)
            candidates = Enumerable.Range(0, _found.Length).Where(i => !_found[i]).ToList();

        if (candidates.Count == 0)
            return -1;

        var index = candidates[random.Next(candidates.Count)];
        _hinted[index] = true;

        return index;
    }
}
