using System;
using System.Numerics;

namespace Sanctuary.Game.VehicleGames.Soccer;

/// <summary>
/// Soccer's rules and the ball's flight, read off the live match: the ball falls at 9.8 m/s², loses about a fifth of
/// its speed a second in the air, bounces back up at seven tenths and settles 0.3 m up (its centre); kicks leave the
/// foot about 2.4 m up at 15 to 22 m/s; a goal is the ball over a goal line in the mouth. Team 0 (the player's)
/// defends the high x end and shoots at the low.
/// </summary>
public static class SoccerRules
{
    public const float Gravity = 9.8f;
    public const float AirDrag = 0.22f;
    public const float Bounce = 0.7f;
    public const float BounceFriction = 0.85f;
    public const float RollingDrag = 1.2f;
    public const float BallRadius = 0.3f;

    /// <summary>
    /// Where a kicked ball leaves the kicker, above their feet.
    /// </summary>
    public const float KickHeight = 2.4f;

    public const float GoalHalfWidth = 3.6f;
    public const float GoalHeight = 2.6f;

    /// <summary>
    /// A loose ball this close (and this low) is a player's.
    /// </summary>
    public const float PickupReach = 1.2f;
    public const float PickupHeight = 1.4f;

    public const float PeriodSeconds = 180f;

    /// <summary>
    /// One step of a loose ball: gravity, drag, the ground, and the pitch's sides (not the goal mouths).
    /// </summary>
    public static (Vector3 Position, Vector3 Velocity, bool Bounced) Step(Vector3 position, Vector3 velocity, float deltaSeconds, Vector4 bounds, float centreZ)
    {
        var bounced = false;

        velocity.Y -= Gravity * deltaSeconds;

        var drag = position.Y > BallRadius + 0.05f ? AirDrag : RollingDrag;
        var horizontal = new Vector2(velocity.X, velocity.Z) * MathF.Max(0f, 1f - drag * deltaSeconds);

        velocity = new Vector3(horizontal.X, velocity.Y, horizontal.Y);
        position += velocity * deltaSeconds;

        if (position.Y < BallRadius)
        {
            position.Y = BallRadius;

            if (velocity.Y < -1f)
            {
                velocity = new Vector3(velocity.X * BounceFriction, -velocity.Y * Bounce, velocity.Z * BounceFriction);
                bounced = true;
            }
            else
            {
                velocity.Y = 0f;
            }
        }

        // The sides bounce it back; the ends do too, outside the goal mouths.
        if (position.Z < bounds.Y || position.Z > bounds.W)
        {
            position.Z = Math.Clamp(position.Z, bounds.Y, bounds.W);
            velocity.Z = -velocity.Z * Bounce;
            bounced = true;
        }

        var inMouth = MathF.Abs(position.Z - centreZ) < GoalHalfWidth && position.Y < GoalHeight;

        if (!inMouth && (position.X < bounds.X || position.X > bounds.Z))
        {
            position.X = Math.Clamp(position.X, bounds.X, bounds.Z);
            velocity.X = -velocity.X * Bounce;
            bounced = true;
        }

        return (position, velocity, bounced);
    }

    /// <summary>
    /// The goal the ball is in, if it's over a goal line inside the mouth: 0 the low x end (team 1's), 1 the high
    /// (team 0's).
    /// </summary>
    public static int? GoalAt(Vector3 position, Vector4 bounds, float centreZ)
    {
        if (MathF.Abs(position.Z - centreZ) >= GoalHalfWidth || position.Y >= GoalHeight)
            return null;

        if (position.X < bounds.X)
            return 0;

        if (position.X > bounds.Z)
            return 1;

        return null;
    }

    /// <summary>
    /// The team that defends a goal (team 0 the high x end), so the other one scores in it.
    /// </summary>
    public static int Defender(int goal) => goal == 1 ? 0 : 1;

    /// <summary>
    /// The x of the goal a team shoots at.
    /// </summary>
    public static float TargetGoalX(int team, Vector4 bounds) => team == 0 ? bounds.X : bounds.Z;

    /// <summary>
    /// A kick from <paramref name="from"/> towards <paramref name="target"/> at <paramref name="speed"/>, with a
    /// little lift: the velocity the ball leaves with.
    /// </summary>
    public static Vector3 Kick(Vector3 from, Vector3 target, float speed, float lift)
    {
        var flat = new Vector3(target.X - from.X, 0f, target.Z - from.Z);

        if (flat.LengthSquared() < 1e-4f)
            flat = Vector3.UnitX;

        return Vector3.Normalize(flat) * speed + new Vector3(0f, lift, 0f);
    }

    /// <summary>
    /// The stick (x, y in -1, 0, 1) that runs a player from one point towards another, and the direction it runs:
    /// stick x is world +x and stick y world -z, as the live AI moved.
    /// </summary>
    public static (float X, float Y, Vector3 Direction) Stick(Vector3 from, Vector3 to)
    {
        var flat = new Vector3(to.X - from.X, 0f, to.Z - from.Z);
        var distance = flat.Length();

        if (distance < 0.6f)
            return (0f, 0f, Vector3.Zero);

        flat /= distance;

        // Eight ways, as a keyboard drives it.
        var x = MathF.Abs(flat.X) > 0.38f ? MathF.Sign(flat.X) : 0f;
        var y = MathF.Abs(flat.Z) > 0.38f ? -MathF.Sign(flat.Z) : 0f;

        return (x, y, StickDirection(x, y));
    }

    public static Vector3 StickDirection(float x, float y)
    {
        var direction = new Vector3(x, 0f, -y);

        return direction.LengthSquared() > 1e-4f ? Vector3.Normalize(direction) : Vector3.Zero;
    }

    public const int GoalPoints = 140_000;
    public const int StealPoints = 25_000;
    public const int KnockdownPoints = 25_000;
    public const int DodgePoints = 30_000;
}
