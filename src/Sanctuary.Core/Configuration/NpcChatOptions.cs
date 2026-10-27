namespace Sanctuary.Core.Configuration;

/// <summary>
/// Lets NPCs answer what players say near them through a Studio Umbra chat bot. Off until an ApiKey is set,
/// e.g. in the git-ignored gateway.local.json or the NpcChat__ApiKey environment variable.
/// </summary>
public class NpcChatOptions
{
    public const string Section = "NpcChat";

    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "https://studioumbra.org/api/v1";

    /// <summary>
    /// A Studio Umbra personal access token (rlm_...) with the write scope.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// A model id from GET /models; empty uses the account's preferred model.
    /// </summary>
    public string? ModelId { get; set; }

    /// <summary>
    /// How close (in metres) the nearest NPC must be to hear a player's /say.
    /// </summary>
    public float HearRange { get; set; } = 10f;

    public int PlayerCooldownSeconds { get; set; } = 3;

    /// <summary>
    /// Server-wide cap; Studio Umbra allows 20 chat messages a minute per token.
    /// </summary>
    public int MaxRepliesPerMinute { get; set; } = 18;

    public int MaxReplyLength { get; set; } = 240;
}
