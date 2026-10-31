using System;

namespace Sanctuary.Database.Entities;

/// <summary>
/// One player's vote on a published house: one each, for as long as the house stays in the directory.
/// </summary>
public class DbHouseVote
{
    public ulong HouseId { get; set; }
    public DbHouseRating Rating { get; set; } = null!;

    public ulong VoterCharacterId { get; set; }

    /// <summary>
    /// 1 to 5.
    /// </summary>
    public int Stars { get; set; }

    public DateTimeOffset Voted { get; set; } = DateTimeOffset.UtcNow;
}
