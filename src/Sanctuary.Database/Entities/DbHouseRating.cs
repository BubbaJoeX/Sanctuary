using System;
using System.Collections.Generic;

namespace Sanctuary.Database.Entities;

/// <summary>
/// A house its owner submitted to the Housing Directory, where others can find it, visit it and vote on it.
/// Removing it from the directory (or deleting the house) loses its votes.
/// </summary>
public class DbHouseRating
{
    public ulong HouseId { get; set; }
    public DbHouse House { get; set; } = null!;

    public DateTimeOffset Published { get; set; } = DateTimeOffset.UtcNow;

    public string? Description { get; set; }
    public string? Keywords { get; set; }

    public ICollection<DbHouseVote> Votes { get; set; } = new HashSet<DbHouseVote>();
}
