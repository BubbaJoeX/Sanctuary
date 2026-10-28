using System.Collections.Generic;

namespace Sanctuary.Game.Minigames.Soe;

/// <summary>
/// A Flash minigame the server plays itself. The SOE movies carry a server of their own for testing outside the
/// game (their GameServer class, on com.sonyonline.game.SoeGameFramework and SoeNetworkServer); in Free Realms
/// the movie's networking points at the real server instead, which ran the same game. This is a port of that
/// framework: games port their GameServer onto it and the movie's client half plays against them unchanged.
/// </summary>
public abstract class SoeGameServer : FlashMinigame
{
    private readonly Dictionary<int, SoeLevel> _levels = [];
    private readonly SoeLevel _emptyLevel;

    private long _lastTick;

    private int _score;
    private int _scoreLevel;

    protected SoeGameServer()
    {
        _emptyLevel = new SoeLevel(this);
        CurrentLevel = _emptyLevel;

        EventQueue = new SoeEventQueue(() => Clock);
        EventQueue.OnEvent = OnEvent;
    }

    public override bool KeepsScore => true;

    /// <summary>
    /// The game's clock in milliseconds (the movie's getTimer).
    /// </summary>
    public long Clock => Now;

    protected SoeEventQueue EventQueue { get; }

    public SoeLevel CurrentLevel { get; private set; }

    /// <summary>
    /// The level number being played, from 1.
    /// </summary>
    public int Level { get; private set; }

    public bool IsStarted { get; private set; }
    public bool IsInProgress { get; private set; }
    public bool IsDone { get; private set; }
    public bool IsConnected { get; private set; }

    private bool _active;

    public bool IsActive => IsInProgress && !IsPaused && _active;

    public bool IsPaused => CurrentLevel.IsPaused;

    public int LevelCount => _levels.Count;

    public int GamesStarted { get; private set; }
    public int GamesWon { get; private set; }
    public int GamesLost { get; private set; }
    public int GamesStopped { get; private set; }

    public override void OnStart()
    {
        _lastTick = Clock;

        Reset();
        GameInitialize();
    }

    public sealed override bool OnMessage(string name, FlashArgs args)
    {
        switch (name)
        {
            case "OnConnectMsg":
                OnConnectMsg();
                return true;

            case "OnStartGameMsg":
                OnStartGameMsg(args.Has(0) ? args.Int(0) : null);
                return true;

            case "OnStartLevelMsg":
                OnStartLevelMsg();
                return true;

            case "OnNextLevelMsg":
                OnNextLevelMsg();
                return true;

            case "OnPauseMsg":
                OnPauseMsg(args.Bool(0));
                return true;
        }

        return OnGameMessage(name, args);
    }

    /// <summary>
    /// The game's own messages (its GameServerNetwork's On*Msg handlers). False for unknown ones.
    /// </summary>
    protected abstract bool OnGameMessage(string name, FlashArgs args);

    /// <summary>
    /// FRServer_Pause: the movie's own pause (its menu, a tutorial, the gap before a level), which stops the
    /// level's timer like the test server's OnPauseMsg.
    /// </summary>
    public override void OnPause(bool paused)
    {
        OnPauseMsg(paused);
    }

    public override void Tick()
    {
        var now = Clock;
        var delta = now - _lastTick;
        _lastTick = now;

        EventQueue.Update();
        GameApplyTick(delta);
    }

    #region Framework (SoeGameFramework)

    public bool Reset()
    {
        CurrentLevel = _emptyLevel;
        Level = 0;
        _score = 0;
        _scoreLevel = 0;
        IsStarted = false;
        IsInProgress = false;
        IsDone = false;
        _active = false;

        return GameReset();
    }

    public bool CheckLevelDone()
    {
        return IsInProgress && CurrentLevel.CheckDone();
    }

    public void SetActive(bool state)
    {
        _active = state;
    }

    public bool Pause()
    {
        if (CurrentLevel == _emptyLevel || !IsInProgress || !CurrentLevel.IsInProgress || CurrentLevel.IsPaused)
            return false;

        if (!CurrentLevel.PauseLevel())
            return false;

        EventQueue.Pause();
        return true;
    }

    public bool Resume()
    {
        if (!CurrentLevel.ResumeLevel())
            return false;

        EventQueue.Resume();
        return true;
    }

    public bool StartGame(int? level = null)
    {
        if (IsStarted)
            return false;

        Reset();

        Level = level ?? 0;
        GamesStarted++;
        IsStarted = true;
        IsInProgress = true;

        OnStartGame();
        return true;
    }

    /// <summary>
    /// Ends the game won, tells the movie (<see cref="OnWon"/>) and pays out.
    /// </summary>
    public bool GameWon()
    {
        if (!IsInProgress)
            return false;

        GamesWon++;
        GameOver();
        OnWon();
        Finish(true);

        return true;
    }

    public bool GameLost()
    {
        if (!IsInProgress)
            return false;

        GamesLost++;
        GameOver();
        OnLost();
        Finish(false);

        return true;
    }

    /// <summary>
    /// Ends the game without a result (quit); nothing is paid.
    /// </summary>
    public bool StopGame(string reason)
    {
        if (!IsInProgress)
            return false;

        GamesStopped++;
        GameOver();
        OnStopGame(reason);

        return true;
    }

    private void GameOver()
    {
        if (IsPaused)
            Resume();

        if (CurrentLevel.IsInProgress)
            CurrentLevel.StopLevel();

        EventQueue.ClearAll();
        IsInProgress = false;
        IsDone = true;
    }

    /// <summary>
    /// The game's running total: what it has banked plus the level being played.
    /// </summary>
    public int GetScore() => _score;

    public void SetLevelScore(int score)
    {
        _scoreLevel = score;
        Score = _score + score;
        SendScore(_scoreLevel, _score + score);
    }

    public void SetScore(int score)
    {
        Score = score;
        SendScore(_scoreLevel, score);
    }

    public int DeltaScore(int delta)
    {
        _score += delta;
        Score = _score + _scoreLevel;
        SendScore(_scoreLevel, _score);

        return _score;
    }

    /// <summary>
    /// Adds a level under its number (from 1).
    /// </summary>
    public void AddLevel(int level, SoeLevel soeLevel)
    {
        _levels[level] = soeLevel;
    }

    public SoeLevel? GetLevel(int level) => _levels.GetValueOrDefault(level);

    /// <summary>
    /// Moves on to the next level (or to <paramref name="level"/>); past the last one, the game is won.
    /// </summary>
    public bool NextLevel(int? level = null)
    {
        if (level is not null)
            Level = level.Value - 1;

        if (!_levels.TryGetValue(Level + 1, out var next))
        {
            CurrentLevel = _emptyLevel;
            GameWon();
            return false;
        }

        CurrentLevel = next;
        Level++;

        OnNextLevel();
        return true;
    }

    public bool StartLevel()
    {
        if (CurrentLevel.IsInProgress)
            return false;

        EventQueue.ClearAll();
        CurrentLevel.StartLevel();

        OnStartLevel();
        return true;
    }

    public bool StopLevel()
    {
        if (!CurrentLevel.IsInProgress)
            return false;

        if (IsPaused)
            Resume();

        CurrentLevel.StopLevel();
        EventQueue.ClearAll();

        OnStopLevel();
        return true;
    }

    public bool FinishLevel()
    {
        if (!CurrentLevel.IsDone)
            return false;

        OnFinishLevel();
        return true;
    }

    public int AddEvent(string type, long timeout, bool recur = false, object? data = null)
    {
        return EventQueue.AddEvent(type, timeout, recur, data);
    }

    public bool CancelEvent(int? id = null)
    {
        if (id is null)
        {
            EventQueue.ClearAll();
            return true;
        }

        return EventQueue.CancelEvent(id.Value);
    }

    public bool TriggerEvent(int? id = null)
    {
        return EventQueue.TriggerEvent(id);
    }

    /// <summary>
    /// Data is ready: tell a connected movie the server is ready.
    /// </summary>
    public void SetReady()
    {
        if (IsConnected)
            SendServerReady();
    }

    #endregion

    #region Messages from the movie

    protected virtual void OnConnectMsg()
    {
        GameClientConnect();
        IsConnected = true;

        if (IsGameReady())
            SendServerReady();
    }

    protected virtual void OnStartGameMsg(int? level)
    {
        Reset();
        StartGame(level);
    }

    protected virtual void OnStartLevelMsg()
    {
        StartLevel();
    }

    protected virtual void OnNextLevelMsg()
    {
        if (CurrentLevel.Goal > CurrentLevel.Score)
        {
            GameLost();
            return;
        }

        FinishLevel();
    }

    protected virtual void OnPauseMsg(bool state)
    {
        if (state)
        {
            if (Pause())
                SendPause(false);
        }
        else if (Resume())
        {
            SendPause(true);
        }
    }

    #endregion

    #region Messages to the movie (SoeNetworkServer)

    protected void SendServerReady() => Send("OnServerReadyMsg");

    protected void SendActive(bool state) => Send("OnActiveMsg", state);

    protected void SendPause(bool state) => Send("OnPauseMsg", state);

    protected void SendLevelHistory(string history) => Send("OnLevelHistoryMsg", history);

    protected void SendNextLevel(int level, int goal, int best) => Send("OnNextLevelMsg", level, goal, best);

    protected void SendStopLevel(int level, int goal, int score, int percent) => Send("OnStopLevelMsg", level, goal, score, percent);

    protected void SendGameStopped() => Send("OnGameStoppedMsg");

    protected void SendGameWon() => Send("OnGameWonMsg");

    protected void SendGameLost() => Send("OnGameLostMsg");

    protected void SendScore(int levelScore, int totalScore) => Send("OnScoreMsg", levelScore, totalScore);

    #endregion

    #region Game hooks (the movie's GameServer overrides)

    protected virtual void GameInitialize()
    {
    }

    protected virtual bool IsGameReady() => true;

    protected virtual bool GameReset() => true;

    protected virtual void GameApplyTick(long delta)
    {
    }

    protected virtual void GameClientConnect()
    {
    }

    protected virtual void OnStartGame()
    {
        NextLevel(1);
    }

    protected virtual void OnNextLevel()
    {
        StartLevel();
    }

    protected virtual void OnStartLevel()
    {
    }

    protected virtual void OnStopLevel()
    {
        FinishLevel();
    }

    protected virtual void OnFinishLevel()
    {
        NextLevel();
    }

    protected virtual void OnStopGame(string reason)
    {
        SendGameStopped();
    }

    protected virtual void OnWon()
    {
        SendGameWon();
    }

    protected virtual void OnLost()
    {
        SendGameLost();
    }

    protected virtual void OnEvent(int id, string type, object? data)
    {
    }

    #endregion
}
