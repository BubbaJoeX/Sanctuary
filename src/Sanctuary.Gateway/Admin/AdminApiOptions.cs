namespace Sanctuary.Gateway.Admin;

/// <summary>
/// The admin panel's link into the running game (see <see cref="AdminApiService"/>).
/// </summary>
public class AdminApiOptions
{
    public const string Section = "AdminApi";

    /// <summary>
    /// The shortest key the API accepts.
    /// </summary>
    public const int MinKeyLength = 24;

    /// <summary>
    /// Where the API listens. Keep it on loopback or a private network: the admin panel's backend is its only client.
    /// </summary>
    public string Urls { get; set; } = "http://127.0.0.1:20050";

    /// <summary>
    /// Shared secret the admin panel's backend sends as a bearer token. The API stays off while this is unset.
    /// </summary>
    public string? Key { get; set; }
}
