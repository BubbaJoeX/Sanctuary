using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Game.Minigames.Mimic;

/// <summary>
/// The "Mimic" crafting and rhythm games (cooking, smelting, Feeling the Beat!, Hack 'n' Slash). Unlike the SOE
/// games the server plays (<see cref="Soe.SoeGameServer"/>), these movies play themselves: each runs its own
/// GameServer over a local network inside the movie, and talks to Free Realms on a second one
/// (GameExternalNetwork) only to get its game data and to report how it went.
/// <para>
/// The flow, as the movie's GameClient runs it: it connects (OnConnectMsg) and asks for its data
/// (FRServer_ReadyForGameData). The server answers with the game's id (OnGameDataMsg), one message per round
/// (OnPhaseDataMsg: a MimicMiniGamePhaseData.txt row), any extra rows the game reads (OnCustomDataMsg) and
/// OnDataComplete, at which the movie builds its rounds and starts. While it plays it reports rounds starting
/// and ending (FRServer_RoundTransitionPause, FRServer_CompleteLevel, FRServer_StopLevel) and its score
/// (FRServer_ScoreSet). It ends with FRServer_GameWon or FRServer_GameLost and then waits: the server's
/// OnGameWonMsg or OnGameLostMsg is what makes it end the game (FRServer_GameOver), which closes it.
/// </para>
/// <para>
/// The client plays these games, so its result is only taken from a game that got its data, played for a
/// believable time and (where the movie reports it) finished every round; its score is clamped.
/// </para>
/// </summary>
public abstract class MimicGame : FlashMinigame
{
    /// <summary>
    /// The rounds of every game of a movie (Resources/Minigames/&lt;Game&gt;/).
    /// </summary>
    public const string PhaseDataFileName = "MimicMiniGamePhaseData.txt";

    /// <summary>
    /// The most a round can believably score; a reported score is clamped to this times the rounds.
    /// </summary>
    public const int MaxScorePerRound = 200_000;

    /// <summary>
    /// The least time a round can take, from the movie getting its data to it reporting the result: every round
    /// has its intro, its animations and its score popup to get through.
    /// </summary>
    public const int MinimumMillisecondsPerRound = 2_000;

    private long? _dataSentAt;
    private bool? _answeredWon;

    /// <summary>
    /// The rounds sent to the movie, one per sequence, in order.
    /// </summary>
    public IReadOnlyList<MimicRow> Phases { get; private set; } = [];

    /// <summary>
    /// Rounds the movie finished (FRServer_RoundTransitionPause true, sent as each round's end screen comes up).
    /// </summary>
    protected int RoundsCompleted { get; private set; }

    /// <summary>
    /// The level the movie last stopped on (FRServer_StopLevel) and whether it called that a win; 0 until then.
    /// </summary>
    protected int StoppedLevel { get; private set; }
    protected bool StoppedWinner { get; private set; }

    /// <summary>
    /// The recipe (level set) the game plays: the GAME column of its rows, and the game id the movie is told.
    /// Copies of a game (the world's or a quest's) share their original's rows through <see cref="Resources.Definitions.MinigameDefinition.Level"/>.
    /// </summary>
    public int RecipeId => Definition.Level > 0 ? Definition.Level : Definition.Id;

    /// <summary>
    /// The table of extra rows the movie asks for, sent as OnCustomDataMsg; none by default.
    /// </summary>
    protected virtual string? CustomDataFileName => null;

    /// <summary>
    /// The data columns of the movie's phase rows (DATA1 on).
    /// </summary>
    protected abstract int DataColumns { get; }

    /// <summary>
    /// Whether the movie told the server, as it played, that it got through every round: what a reported win needs
    /// besides the time it took.
    /// </summary>
    protected abstract bool PlayedEveryRound { get; }

    /// <summary>
    /// The least time a game takes to reach its end, from its data to its result: its rounds can't go quicker.
    /// </summary>
    private long MinimumPlayMilliseconds => Phases.Count * (long)MinimumMillisecondsPerRound;

    /// <summary>
    /// The least time a game takes to be won, which can be longer (a song plays to its end).
    /// </summary>
    protected virtual long MinimumWinMilliseconds => MinimumPlayMilliseconds;

    public override bool OnMessage(string name, FlashArgs args)
    {
        switch (name)
        {
            case "OnConnectMsg":
            case "FRServer_Connect":
                // The movie's network to Free Realms is up; it asks for its data next.
                return true;

            case "FRServer_ReadyForGameData":
                SendGameData();
                return true;

            case "FRServer_RoundTransitionPause":
                if (args.Bool(0))
                    RoundsCompleted++;
                return true;

            case "FRServer_CompleteLevel":
                // A round scored (the base GameClient's showRoundFeedback); a failed round doesn't send it.
                return true;

            case "FRServer_StopLevel":
                StoppedLevel = Number(args, 0);
                StoppedWinner = Number(args, 1) != 0;
                return true;

            case "FRServer_StatUpdate":
                // Counts of good and bad actions for the Free Realms stat tracking; nothing reads them here.
                return true;

            case "FRServer_GameWon":
            case "FRServer_GameLost":
                OnResult(name == "FRServer_GameWon", args);
                return true;

            case "FRServer_GameOver":
            case "FRServer_GameClose":
            case "FRServer_GameQuit":
                // MinigameManager closes the movie.
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks the game's rows against what the movie can play, as notes for whoever authors them; none when
    /// they're fine. The base checks the layout: numbers only, every column the movie reads, rounds 1 to N.
    /// </summary>
    public IReadOnlyList<string> CheckData()
    {
        var (phases, custom) = LoadData();
        var problems = new List<string>();

        if (phases.Count == 0)
            problems.Add($"{PhaseDataFileName} has no rows for game {RecipeId}.");

        foreach (var row in phases.Concat(custom))
        {
            if (!row.IsNumeric)
                problems.Add($"Row {row}: every column must be a whole number.");
        }

        foreach (var row in phases)
        {
            if (row.Count < MimicRow.FirstDataColumn + DataColumns)
                problems.Add($"Row {row}: the movie reads {MimicRow.FirstDataColumn + DataColumns} columns.");
        }

        var sequences = phases.Select(x => x.Sequence).Distinct().Order().ToList();

        if (!sequences.SequenceEqual(Enumerable.Range(1, sequences.Count)))
            problems.Add($"Game {RecipeId}: its rounds must be numbered 1 to {sequences.Count} ({string.Join(", ", sequences)}).");

        // Every play the rows allow: the first row of each round, and each alternative in its place.
        var rounds = phases.GroupBy(x => x.Sequence).OrderBy(x => x.Key).Select(x => x.ToList()).ToList();
        var first = rounds.Select(x => x[0]).ToList();
        var plays = new List<List<MimicRow>> { first };

        for (var i = 0; i < rounds.Count; i++)
        {
            foreach (var alternative in rounds[i].Skip(1))
            {
                var play = first.ToList();
                play[i] = alternative;
                plays.Add(play);
            }
        }

        problems.AddRange(plays.SelectMany(x => CheckRounds(x, custom)).Distinct());

        return problems;
    }

    /// <summary>
    /// The game's own checks of its rounds (one row per round, in order) and extra rows.
    /// </summary>
    protected abstract IEnumerable<string> CheckRounds(IReadOnlyList<MimicRow> rounds, IReadOnlyList<MimicRow> custom);

    /// <summary>
    /// Answers FRServer_ReadyForGameData, once: the movie adds every row it's sent to what it has, so a second copy
    /// would play each round twice.
    /// </summary>
    private void SendGameData()
    {
        if (_dataSentAt is not null)
            return;

        var (phases, custom) = LoadData();

        // Rows sharing a round are alternatives: one of them, picked for this play.
        Phases = phases
            .GroupBy(x => x.Sequence)
            .OrderBy(x => x.Key)
            .Select(x => x.ElementAt(Random.Shared.Next(x.Count())))
            .ToList();

        if (Phases.Count == 0)
        {
            // With no rounds the movie ends its game as soon as it starts, which closes it.
            Logger.LogError("Minigame {id} ({name}) has no rounds for game {recipe} in {file}.", Definition.Id,
                Definition.Comment, RecipeId, Path.Combine(DataDirectory, PhaseDataFileName));
        }

        Send("OnGameDataMsg", RecipeId);

        foreach (var phase in Phases)
            Send("OnPhaseDataMsg", phase.ToString());

        foreach (var row in custom)
            Send("OnCustomDataMsg", row.ToString());

        Send("OnDataComplete");

        if (Phases.Count > 0)
            _dataSentAt = Now;
    }

    private (List<MimicRow> Phases, List<MimicRow> Custom) LoadData()
    {
        var phases = MimicRow.ReadTable(Path.Combine(DataDirectory, PhaseDataFileName))
            .Where(x => x.Game == RecipeId)
            .ToList();

        var custom = CustomDataFileName is null
            ? []
            : MimicRow.ReadTable(Path.Combine(DataDirectory, CustomDataFileName)).Where(x => x.Game == RecipeId).ToList();

        return (phases, custom);
    }

    /// <summary>
    /// FRServer_GameWon or FRServer_GameLost: pays the result and tells the movie to end its game. The first result
    /// is the game's; a repeat only gets the same answer.
    /// </summary>
    private void OnResult(bool reportedWin, FlashArgs args)
    {
        Score = Math.Clamp(args.Int(0), 0, MaxScorePerRound * Math.Max(1, Phases.Count));

        if (_answeredWon is { } answered)
        {
            SendEnd(answered);
            return;
        }

        if (_dataSentAt is not { } dataSentAt)
        {
            Logger.LogWarning("{player} reported a result for minigame {id} ({name}), which never got its rounds; nothing paid.",
                PlayerName, Definition.Id, Definition.Comment);
            SendEnd(false);
            return;
        }

        var played = Now - dataSentAt;

        if (played < (reportedWin ? MinimumWinMilliseconds : MinimumPlayMilliseconds))
        {
            Logger.LogWarning("{player} reported a result for minigame {id} ({name}) after {played} ms of {rounds} rounds; nothing paid.",
                PlayerName, Definition.Id, Definition.Comment, played, Phases.Count);
            SendEnd(false);
            return;
        }

        var won = reportedWin && PlayedEveryRound;

        if (reportedWin && !won)
        {
            Logger.LogWarning("{player} reported a win of minigame {id} ({name}) without finishing its {rounds} rounds; paid as a loss.",
                PlayerName, Definition.Id, Definition.Comment, Phases.Count);
        }

        Finish(won);
        SendEnd(won);
    }

    /// <summary>
    /// OnGameWonMsg or OnGameLostMsg: the movie ends its game (GameClient.EndOfGame), which sends FRServer_GameOver.
    /// </summary>
    private void SendEnd(bool won)
    {
        _answeredWon = won;
        Send(won ? "OnGameWonMsg" : "OnGameLostMsg");
    }

    private string PlayerName => Player is null ? "A detached game" : Player.Name.FullName;

    /// <summary>
    /// A number the movie may have sent with the closing parenthesis of its own command still attached
    /// (SoeInterfaceFreeRealms.StopLevel and UpdateStats leave their quote open).
    /// </summary>
    private static int Number(FlashArgs args, int index)
    {
        var text = args.String(index).TrimEnd(')', '"', ' ');

        return new FlashArgs([text]).Int(0);
    }
}
