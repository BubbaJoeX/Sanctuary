using System.ComponentModel.DataAnnotations;

namespace Sanctuary.Core.Configuration;

public sealed class LoginServerOptions : ServerOptions
{
    [Required]
    public required int LoginGatewayPort { get; set; }

    [Required]
    public required string LoginGatewayChallenge { get; set; }

    /// <summary>
    /// Locks the server and only allows admins to login.
    /// </summary>
    public bool IsLocked { get; set; }

    public int DefaultTitleId { get; set; }

    [Required]
    public required int DefaultProfileId { get; set; }

    public int StartingCoins { get; set; }

    public bool UnlockAllTitles { get; set; }
    public bool UnlockAllProfiles { get; set; }

    /// <summary>
    /// The rank a new character's jobs start at: 1 for jobs earned from scratch, 20 to have every job maxed out
    /// for testing.
    /// </summary>
    public int StartingProfileRank { get; set; } = 1;
}