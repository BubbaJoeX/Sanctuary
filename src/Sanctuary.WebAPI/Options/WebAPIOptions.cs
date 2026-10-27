namespace Sanctuary.WebAPI.Options;

public class WebAPIOptions
{
    public const string Section = "WebAPI";

    public string? LaunchArguments { get; set; }

    /// <summary>
    /// Local mirror of the asset server (<c>manifest.txt.z</c> plus <c>000</c>-<c>999</c> bucket folders).
    /// </summary>
    public string? AssetsDirectory { get; set; }

    /// <summary>
    /// Asset server used for anything not in <see cref="AssetsDirectory"/> or the client mods.
    /// </summary>
    public string? AssetUpstreamAddress { get; set; }

    /// <summary>
    /// Folder (relative to the content root) holding modified client assets, e.g. the GM panel.
    /// </summary>
    public string? ClientModsDirectory { get; set; }

    /// <summary>
    /// Launcher server whose <c>clientmanifest.xml</c> and <c>client/</c> files are used for anything not in the client patches.
    /// </summary>
    public string? ClientUpstreamAddress { get; set; }

    /// <summary>
    /// Folder (relative to the content root) holding modified client files laid out like the client folder,
    /// e.g. <c>locale/en_us_data.dat</c>, sent to launchers in place of the upstream files.
    /// </summary>
    public string? ClientPatchesDirectory { get; set; }

    /// <summary>
    /// What the launcher shows for this server; <c>servermanifest.xml</c> is only served when this is set.
    /// </summary>
    public ServerManifestOptions? ServerManifest { get; set; }
}

public class ServerManifestOptions
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Base address of this WebAPI, for the launcher's login and register requests.
    /// </summary>
    public string? WebApiUrl { get; set; }

    /// <summary>
    /// Login server as <c>host:port</c>.
    /// </summary>
    public string? LoginServer { get; set; }
}