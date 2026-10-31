using System.Numerics;

using Sanctuary.Game.Entities;

namespace Sanctuary.Game.VehicleGames.Soccer;

/// <summary>
/// One of the eight on the pitch: the player (whose client moves them and reports where and what they're doing) or
/// an AI player the server drives by its stick and buttons, as live.
/// </summary>
public sealed class SoccerPlayer(ulong guid, int team, int position, Vector3 kickOff)
{
    public ulong Guid { get; } = guid;

    public int Team { get; } = team;

    /// <summary>
    /// 0-2 in the field, 3 the goalie.
    /// </summary>
    public int Position { get; } = position;

    public bool IsGoalie => Position == 3;

    public Vector3 KickOff { get; } = kickOff;

    public ArenaParticipant? Participant { get; init; }

    public bool IsPlayer => Participant is not null;

    public Npc? Npc { get; set; }

    public float RunSpeed { get; set; } = 6.65f;

    public Vector3 Location { get; set; } = kickOff;

    /// <summary>
    /// Which way they face (flat, normalised).
    /// </summary>
    public Vector3 Facing { get; set; }

    #region Controls and state

    public float StickX { get; set; }
    public float StickY { get; set; }
    public uint Buttons { get; set; }
    public bool ControlsChanged { get; set; }

    /// <summary>
    /// Their animation state (1 idle, 2 run, 11 run with the ball, 17 kick, 35 steal ...).
    /// </summary>
    public byte State { get; set; } = 1;
    public byte Charge { get; set; }
    public uint Sequence { get; set; }
    public bool StateChanged { get; set; }

    /// <summary>
    /// Busy with a kick, a tackle or getting up until then: no running.
    /// </summary>
    public long BusyUntil { get; set; }

    public long NextTackleAt { get; set; }
    public long HeldSince { get; set; }

    public long PositionSentAt { get; set; }

    #endregion

    #region Stats

    public int Knockdowns { get; set; }
    public int Steals { get; set; }
    public int Dodges { get; set; }
    public int Goals { get; set; }
    public int Score { get; set; }
    public bool StatsChanged { get; set; }

    /// <summary>
    /// The power-up they have (1 speed shoes, 2 toughness, 4 quick charge), and until when.
    /// </summary>
    public int PickupKind { get; set; }
    public long PickupUntil { get; set; }

    #endregion

    public void SetState(byte state)
    {
        if (State == state)
            return;

        State = state;
        Sequence++;
        StateChanged = true;
    }

    public void SetStick(float x, float y, uint buttons = 0)
    {
        if (StickX == x && StickY == y && Buttons == buttons)
            return;

        StickX = x;
        StickY = y;
        Buttons = buttons;
        ControlsChanged = true;
    }
}

/// <summary>
/// Soccer's ball: loose and flying (the server's to simulate), or held.
/// </summary>
public sealed class SoccerBall
{
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }

    /// <summary>
    /// Who has it, or 0 when it's loose.
    /// </summary>
    public ulong Holder { get; set; }

    public bool IsLoose => Holder == 0;

    /// <summary>
    /// Lobbed (the client's high arc flag).
    /// </summary>
    public bool Lobbed { get; set; }

    /// <summary>
    /// The live ball updates' decaying kick value: 9.75 off the foot, down by 0.76 an update.
    /// </summary>
    public float KickTimer { get; set; }

    /// <summary>
    /// Last touched by, for the goal.
    /// </summary>
    public ulong LastTouch { get; set; }

    public bool InPlay { get; set; }

    public long UpdateSentAt { get; set; }
}
