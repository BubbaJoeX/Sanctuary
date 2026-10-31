using System;
using System.Numerics;

using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.VehicleGames;

/// <summary>
/// The 3D games' rules that need no zone or packets, so they can be tested on their own.
/// </summary>
public static class ArenaRules
{
    /// <summary>
    /// What a place pays, as shares of the stage's stars and coins: 1st everything, then less down the order. Soccer
    /// pays the winning team in full and the losers a share.
    /// </summary>
    public static (int Stars, int Coins) Payout(VehicleGameStageDefinition stage, int place, bool won)
    {
        var (starShare, coinShare) = stage.Kind == VehicleGameKind.Soccer
            ? won ? (1f, 1f) : place == 0 ? (0.6f, 0.5f) : (0.4f, 0f)
            : place switch
            {
                0 => (1f, 1f),
                1 => (0.75f, 0.6f),
                2 => (0.5f, 0.3f),
                _ => (0.25f, 0f)
            };

        return ((int)MathF.Round(stage.Stars * starShare), (int)MathF.Round(stage.Coins * coinShare));
    }

    /// <summary>
    /// A car's or kart's transform from its position, which way it faces (forward is (sin, 0, cos) of the heading)
    /// and the ground's slope under it: its rotation rows (right, up, forward) and its position.
    /// </summary>
    public static Matrix4x4 Transform(Vector3 position, Vector3 forward, Vector3 up)
    {
        up = Vector3.Normalize(up);
        var right = Vector3.Normalize(Vector3.Cross(up, forward));
        forward = Vector3.Cross(right, up);

        return new Matrix4x4(
            right.X, right.Y, right.Z, 0f,
            up.X, up.Y, up.Z, 0f,
            forward.X, forward.Y, forward.Z, 0f,
            position.X, position.Y, position.Z, 1f);
    }

    public static Vector3 Forward(float heading) => new(MathF.Sin(heading), 0f, MathF.Cos(heading));

    public static float Heading(Vector3 forward) => MathF.Atan2(forward.X, forward.Z);

    /// <summary>
    /// The shortest turn from one heading to another, in -pi to pi.
    /// </summary>
    public static float AngleBetween(float from, float to)
    {
        var angle = (to - from) % MathF.Tau;

        if (angle > MathF.PI)
            angle -= MathF.Tau;
        else if (angle < -MathF.PI)
            angle += MathF.Tau;

        return angle;
    }
}
