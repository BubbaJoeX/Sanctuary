using System;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Game.Minigames.TowerDefense;

/// <summary>
/// The tower defense games (towerdefense.swf: "Penguin Produce", "Weed Killing!", "Protect the Hive", ...). The
/// movie plays the whole game itself: its networking is local (Game.networking is SoeNetwork.TYPE_LOCAL), so its
/// own GameServer runs inside it, and in Free Realms that server never loads data. What the movie needs from the
/// real server is its game: it asks with FRServer_ReadyForGameData and waits for the game id, the map row, a row per
/// tower rank and enemy, and OnGameDataCompleteMsg, which starts it (Game.OnGameDataMsg through
/// OnGameDataCompleteMsg). From then on it only reports, through FRApi: stages, lives, towers, kills, the score and
/// the result.
/// <para>
/// The catalog's Level is the game id, which picks the map (world, map, money, lives, stages and difficulty) and
/// its towers and waves from <see cref="TowerDefenseData"/>. The movie's word on a win is checked against its
/// own reports: it sends FRServer_CompletedStage for each stage as it moves on, the last one just before
/// FRServer_GameWon.
/// </para>
/// </summary>
[FlashMinigame("TowerDefense")]
public sealed class TowerDefenseGame : FlashMinigame
{
    private bool _dataSent;

    public TowerDefenseSetup? Setup { get; private set; }

    /// <summary>
    /// The game id the movie plays (its ConfigServer.mGameIDFromServer): the catalog's Level, else its id.
    /// </summary>
    public int GameId => Definition.Level > 0 ? Definition.Level : Definition.Id;

    /// <summary>
    /// The last stage the movie moved past (FRServer_CompletedStage); it reports 0 as the first wave starts.
    /// </summary>
    public int StagesCompleted { get; private set; }

    /// <summary>
    /// Lives (fish, in Penguin Wars) left, as the movie last reported them (FRServer_LivesCount).
    /// </summary>
    public int Lives { get; private set; }

    public int EnemiesDefeated { get; private set; }
    public int TowersBuilt { get; private set; }
    public int TowersUpgraded { get; private set; }
    public int TowersSold { get; private set; }
    public int PowerUpsCollected { get; private set; }
    public int PowerUpsUsed { get; private set; }

    public override void OnStart()
    {
        Setup = TowerDefenseData.Load(DataDirectory).Resolve(GameId);

        if (Setup is null)
        {
            Logger.LogError("Tower defense game {game} ({name}) has no map, towers or enemies in \"{directory}\".",
                GameId, Definition.Comment, DataDirectory);
            return;
        }

        Lives = Setup.Lives;
    }

    public override bool OnMessage(string name, FlashArgs args)
    {
        switch (name)
        {
            case "OnConnectMsg":
                // FRServer_Connect: the movie is up. It answers itself, from the server inside it.
                return true;

            case "FRServer_ReadyForGameData":
                SendGameData();
                return true;

            case "OnPauseMsg":
                // FR_PauseToggle: its tutorial, a script or its pause button. Nothing here runs on a clock.
                return true;

            case "FRServer_CompletedStage":
                StagesCompleted = Math.Max(StagesCompleted, args.Int(0));
                return true;

            case "FRServer_LivesCount":
                Lives = args.Int(0);
                return true;

            case "FRServer_EnemyDied":
                EnemiesDefeated++;
                return true;

            case "FRServer_TowerBuilt":
                TowersBuilt++;
                return true;

            case "FRServer_TowerUpgraded":
                TowersUpgraded++;
                return true;

            case "FRServer_TowerSold":
                TowersSold++;
                return true;

            case "FRServer_PowerUpCollected":
                PowerUpsCollected++;
                return true;

            case "FRServer_PowerUpUsed":
                PowerUpsUsed++;
                return true;

            case "FRServer_MoneySpent":
            case "FRServer_MoneyEarned":
                // The movie keeps the money; a sale reports a negative spend.
                return true;

            case "FRServer_GameWon":
            {
                Score = args.Int(0);

                var won = IsCompleteWin();

                if (!won)
                {
                    Logger.LogWarning("Tower defense game {game} ({name}) reported a win after {stages} of {total} stages with {lives} lives; paying it as a loss.",
                        GameId, Definition.Comment, StagesCompleted, Setup?.Stages ?? 0, Lives);
                }

                End(won);
                return true;
            }

            case "FRServer_GameLost":
                Score = args.Int(0);
                End(false);
                return true;

            case "FRServer_GameOver":
                // The movie has shown its ending. Penguin Wars (TD_SnowGame) ends a game whose fish are all gone
                // here, without FRServer_GameLost.
                if (!Finished)
                    End(false);

                return true;

            case "FRServer_GameClose":
            case "FRServer_GameQuit":
                // MinigameManager closes the movie.
                return true;
        }

        return false;
    }

    /// <summary>
    /// Answers FRServer_ReadyForGameData, once: the movie adds every row it gets to its map, so a second copy would
    /// double its towers and enemies.
    /// </summary>
    private void SendGameData()
    {
        if (_dataSent)
            return;

        if (Setup is null)
        {
            Logger.LogWarning("Tower defense game {game} ({name}) asked for its data, which didn't load.", GameId, Definition.Comment);
            return;
        }

        _dataSent = true;

        Send("OnGameDataMsg", GameId);
        Send("OnMapDataMsg", Setup.MapRow);

        foreach (var tower in Setup.TowerRows)
            Send("OnTowerDataMsg", tower);

        foreach (var enemy in Setup.EnemyRows)
            Send("OnEnemyDataMsg", enemy);

        Send("OnGameDataCompleteMsg");
    }

    /// <summary>
    /// A win the movie could have sent: every stage moved past (TD_Game.nextLevel reports the last stage, then wins)
    /// and lives left (it loses at none).
    /// </summary>
    private bool IsCompleteWin()
    {
        return Setup is not null && StagesCompleted >= Setup.Stages && Lives > 0;
    }

    private void End(bool won)
    {
        if (Finished)
            return;

        Logger.LogInformation("Tower defense game {game} ({name}) {result} with {score}: {stages}/{total} stages, {lives} lives, {enemies} enemies, {built} towers built, {upgraded} upgraded, {sold} sold, {powerUps} power-ups used.",
            GameId, Definition.Comment, won ? "won" : "lost", Score, StagesCompleted, Setup?.Stages ?? 0, Lives, EnemiesDefeated,
            TowersBuilt, TowersUpgraded, TowersSold, PowerUpsUsed);

        Finish(won);
    }
}
