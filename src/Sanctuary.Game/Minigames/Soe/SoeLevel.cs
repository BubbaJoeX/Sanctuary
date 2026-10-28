namespace Sanctuary.Game.Minigames.Soe;

/// <summary>
/// A level of a <see cref="SoeGameServer"/>: its timer, goal and score. Port of the movies'
/// com.sonyonline.game.SoeLevel, timed on the game's clock (the movie's getTimer). The movie keeps the start and
/// pause times static, shared by every level; one level runs at a time, so they're per level here.
/// </summary>
public class SoeLevel
{
    public const long NotDone = -1;

    private long _startTime;
    private long _pause;
    private long _endTime;

    private bool _forceDone;

    /// <summary>
    /// Milliseconds the level lasts (<see cref="NotDone"/> for no limit).
    /// </summary>
    private long _endDuration = NotDone;
    private long _duration;

    public SoeLevel(SoeGameServer game)
    {
        Game = game;
    }

    public SoeGameServer Game { get; }

    public int Level { get; set; }

    public int Goal { get; set; }

    public int Score { get; private set; }

    public bool IsStarted { get; private set; }

    public bool IsInProgress { get; private set; }

    public bool IsDone { get; private set; }

    public bool IsDoneImmediate { get; set; }

    public bool IsPaused => _pause != 0;

    /// <summary>
    /// The level's length in seconds.
    /// </summary>
    public double EndDuration => _endDuration / 1000.0;

    public void SetEndTime(double seconds)
    {
        _endDuration = (long)(seconds * 1000);
    }

    /// <summary>
    /// Adds (or takes) seconds from a running level's timer.
    /// </summary>
    public void ChangeTimerDuration(double seconds)
    {
        if (!IsStarted || IsDone)
            return;

        _duration += (long)(seconds * 1000);
    }

    public void StartLevel()
    {
        IsStarted = true;
        IsInProgress = true;
        IsDone = false;
        _forceDone = false;

        SetScore(0);

        _pause = 0;
        _endTime = 0;
        _startTime = NonZeroNow();
        _duration = _endDuration;

        OnStartLevel();
    }

    public bool PauseLevel()
    {
        if (!IsInProgress || IsPaused)
            return false;

        _pause = NonZeroNow();
        OnPauseLevel();

        return true;
    }

    public bool ResumeLevel()
    {
        if (!IsPaused)
            return false;

        _startTime += Game.Clock - _pause;
        _pause = 0;
        OnResumeLevel();

        return true;
    }

    public void SetDoneFlag()
    {
        _forceDone = true;
    }

    public bool StopLevel()
    {
        if (!IsInProgress)
            return false;

        if (IsPaused)
        {
            _startTime += Game.Clock - _pause;
            _pause = 0;
        }

        _endTime = Game.Clock;
        _duration = _endTime - _startTime;
        IsDone = true;
        IsInProgress = false;

        OnStopLevel();

        return true;
    }

    /// <summary>
    /// Milliseconds played: the whole level once done.
    /// </summary>
    public long GetDuration()
    {
        if (!IsStarted)
            return 0;

        if (IsDone)
            return _duration;

        if (IsPaused)
            return _pause - _startTime;

        return Game.Clock - _startTime;
    }

    /// <summary>
    /// Milliseconds left on the timer, or <see cref="NotDone"/> without one.
    /// </summary>
    public long GetTimeLeft()
    {
        if (_endDuration == NotDone)
            return NotDone;

        return System.Math.Max(0, _duration - GetDuration());
    }

    public bool CheckDone()
    {
        if (!IsInProgress || IsPaused)
            return false;

        if (_forceDone)
            return true;

        if (_endDuration == NotDone)
            return false;

        if (Game.Clock - _startTime < _duration)
            return false;

        return OnCheckDone();
    }

    public void SetScore(int score)
    {
        Score = score;
        Game.SetLevelScore(score);
    }

    public void DeltaScore(int delta)
    {
        Score += delta;
        Game.SetLevelScore(Score);
    }

    public virtual void OnStartLevel()
    {
    }

    public virtual void OnPauseLevel()
    {
    }

    public virtual void OnResumeLevel()
    {
    }

    public virtual void OnStopLevel()
    {
    }

    public virtual bool OnCheckDone() => true;

    /// <summary>
    /// The movie treats a pause time of 0 as "not paused", so a level started at the clock's first millisecond
    /// would never pause.
    /// </summary>
    private long NonZeroNow() => System.Math.Max(1, Game.Clock);
}
