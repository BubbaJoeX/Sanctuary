using System;

namespace Sanctuary.Game;

/// <summary>
/// The game's time of day as the server syncs it to clients (PacketGameTimeSync): game seconds since an epoch,
/// eight to each real second, so a game day passes in three real hours.
/// </summary>
public static class GameClock
{
    public const int ServerRate = 8;
    public const long Epoch = 1221361670;

    private const int SecondsPerDay = 24 * 60 * 60;

    /// <summary>
    /// Game seconds since <see cref="Epoch"/>, what PacketGameTimeSync sends.
    /// </summary>
    public static long Time => ServerRate * (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - Epoch);

    /// <summary>
    /// The hour of the game day, 0 to just under 24.
    /// </summary>
    public static float Hour => Time % SecondsPerDay / 3600f;
}
