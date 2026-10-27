using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sanctuary.WebAPI.Options;

namespace Sanctuary.WebAPI.Endpoints;

/// <summary>
/// Client files for the OSFR launcher, when this server's address is added to it.
/// </summary>
/// <remarks>
/// The launcher reads <c>servermanifest.xml</c> for the server's name and addresses, checks every file listed
/// in <c>clientmanifest.xml</c> against its client folder (size and XXHash64), and downloads any that don't match
/// from <c>client/&lt;folder&gt;/&lt;file&gt;</c>. Both come from the upstream launcher server. Files in the client
/// patches folder replace (or add) the client file at the same path and are patched into the served manifest,
/// so launchers pick them up on their next verify.
/// </remarks>
public static class ClientEndpoints
{
    private const string HttpClientName = "ClientUpstream";

    private const string ClientManifestFileName = "clientmanifest.xml";
    private const string ServerManifestFileName = "servermanifest.xml";

    /// <summary>
    /// The launcher's current server manifest version.
    /// </summary>
    private const int ServerManifestVersion = 2;

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    private static ILogger _logger = null!;

    private static readonly SemaphoreSlim _manifestLock = new(1, 1);
    private static PatchedManifest? _manifest;

    private sealed record PatchedManifest(string Key, byte[] Xml);

    /// <summary>
    /// A file in the client patches folder, by its '/'-separated path in the client folder.
    /// </summary>
    private sealed record ClientPatch(string Path, FileInfo File);

    public static void AddClientEndpointServices(this IServiceCollection services)
    {
        services.AddHttpClient(HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
    }

    public static void MapClientEndpoints(this WebApplication app)
    {
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();

        _logger = loggerFactory.CreateLogger(nameof(ClientEndpoints));

        app.MapGet("/" + ServerManifestFileName, ServerManifestHandler);
        app.MapGet("/" + ClientManifestFileName, ClientManifestHandlerAsync);
        app.MapGet("/client/{**path}", ClientFileHandlerAsync);
    }

    private static IResult ServerManifestHandler(IOptionsSnapshot<WebAPIOptions> webAPIOptions)
    {
        var serverManifest = webAPIOptions.Value.ServerManifest;

        if (serverManifest is null
            || string.IsNullOrEmpty(serverManifest.Name)
            || string.IsNullOrEmpty(serverManifest.WebApiUrl)
            || string.IsNullOrEmpty(serverManifest.LoginServer))
            return Results.NotFound();

        var document = new XDocument(
            new XElement("ServerManifest",
                new XAttribute("version", ServerManifestVersion),
                new XElement("Name", serverManifest.Name),
                new XElement("Description", serverManifest.Description ?? string.Empty),
                new XElement("WebApiUrl", serverManifest.WebApiUrl),
                new XElement("LoginServer", serverManifest.LoginServer)));

        return Results.Bytes(ToBytes(document), "text/xml");
    }

    private static async Task<IResult> ClientManifestHandlerAsync(
        IWebHostEnvironment environment,
        IOptionsSnapshot<WebAPIOptions> webAPIOptions,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        var manifest = await GetManifestAsync(environment, webAPIOptions.Value, httpClientFactory, cancellationToken);

        return manifest is null
            ? Results.StatusCode(StatusCodes.Status502BadGateway)
            : Results.Bytes(manifest.Xml, "text/xml");
    }

    private static async Task<IResult> ClientFileHandlerAsync(
        string path,
        HttpContext context,
        IWebHostEnvironment environment,
        IOptionsSnapshot<WebAPIOptions> webAPIOptions,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        var segments = path.Split('/');

        // Only plain "<folder>/.../<file>" paths inside the client folder are valid.
        if (segments.Any(x => x is "" or "." or ".." || x.IndexOfAny(InvalidFileNameChars) >= 0))
            return Results.BadRequest();

        var options = webAPIOptions.Value;

        var clientPatch = GetClientPatches(GetClientPatchesDirectory(environment, options))
            .FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

        if (clientPatch is not null)
        {
            _logger.LogInformation("Serving client patch {path}.", clientPatch.Path);

            return Results.File(clientPatch.File.FullName, "application/octet-stream", enableRangeProcessing: true);
        }

        if (string.IsNullOrEmpty(options.ClientUpstreamAddress))
            return Results.NotFound();

        var url = $"{options.ClientUpstreamAddress.TrimEnd('/')}/client/{string.Join('/', segments.Select(Uri.EscapeDataString))}";

        try
        {
            var httpClient = httpClientFactory.CreateClient(HttpClientName);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            // Pass ranges on so the launcher's downloader can resume large files.
            if (context.Request.Headers.Range.Count > 0)
                request.Headers.TryAddWithoutValidation("Range", context.Request.Headers.Range.ToString());

            // Streamed rather than buffered: some client files are tens of megabytes.
            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            context.Response.RegisterForDispose(response);

            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                return Results.StatusCode(StatusCodes.Status416RangeNotSatisfiable);

            if (!response.IsSuccessStatusCode)
                return Results.NotFound();

            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.ContentType = "application/octet-stream";
            context.Response.ContentLength = response.Content.Headers.ContentLength;

            if (response.Content.Headers.ContentRange is { } contentRange)
                context.Response.Headers.ContentRange = contentRange.ToString();

            if (response.Headers.AcceptRanges.Count > 0)
                context.Response.Headers.AcceptRanges = string.Join(", ", response.Headers.AcceptRanges);

            await response.Content.CopyToAsync(context.Response.Body, cancellationToken);

            return Results.Empty;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            // The launcher went away (its downloader cancels and retries); nothing to report.
            if (cancellationToken.IsCancellationRequested)
                return Results.Empty;

            _logger.LogWarning(ex, "Failed to fetch client file {url}.", url);

            if (context.Response.HasStarted)
                return Results.Empty;

            context.Response.Clear();

            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }
    }

    private static string GetClientPatchesDirectory(IWebHostEnvironment environment, WebAPIOptions options)
    {
        return Path.Combine(environment.ContentRootPath, options.ClientPatchesDirectory ?? "ClientPatches");
    }

    private static List<ClientPatch> GetClientPatches(string clientPatchesDirectory)
    {
        if (!Directory.Exists(clientPatchesDirectory))
            return [];

        // Skips hidden and system files (Thumbs.db, desktop.ini, dot files).
        var enumerationOptions = new EnumerationOptions { RecurseSubdirectories = true };

        return new DirectoryInfo(clientPatchesDirectory).EnumerateFiles("*", enumerationOptions)
            .Select(x => new ClientPatch(Path.GetRelativePath(clientPatchesDirectory, x.FullName).Replace(Path.DirectorySeparatorChar, '/'), x))
            .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<byte[]?> GetUpstreamAsync(WebAPIOptions options, IHttpClientFactory httpClientFactory, string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(options.ClientUpstreamAddress))
            return null;

        var url = $"{options.ClientUpstreamAddress.TrimEnd('/')}/{path}";

        try
        {
            var httpClient = httpClientFactory.CreateClient(HttpClientName);

            using var response = await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Failed to fetch {url}.", url);
            return null;
        }
    }

    private static async Task<PatchedManifest?> GetManifestAsync(IWebHostEnvironment environment, WebAPIOptions options,
        IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        var clientPatches = GetClientPatches(GetClientPatchesDirectory(environment, options));

        // Rebuild whenever the upstream server or any patch changes.
        var key = string.Join('|', clientPatches.Select(x => $"{x.Path}:{x.File.Length}:{x.File.LastWriteTimeUtc.Ticks}"))
            + $"|{options.ClientUpstreamAddress}";

        if (_manifest?.Key == key)
            return _manifest;

        await _manifestLock.WaitAsync(cancellationToken);

        try
        {
            if (_manifest?.Key == key)
                return _manifest;

            var source = await GetUpstreamAsync(options, httpClientFactory, ClientManifestFileName, cancellationToken);

            if (source is null)
            {
                _logger.LogError("No client manifest available upstream.");
                return null;
            }

            XDocument document;

            try
            {
                using var stream = new MemoryStream(source);

                document = XDocument.Load(stream);
            }
            catch (XmlException ex)
            {
                _logger.LogError(ex, "The upstream client manifest isn't valid XML.");
                return null;
            }

            if (document.Root?.Element("Folder") is not { } rootFolder)
            {
                _logger.LogError("The upstream client manifest has no root folder.");
                return null;
            }

            var patched = 0;

            foreach (var clientPatch in clientPatches)
            {
                // The launcher reads sizes as 32-bit.
                if (clientPatch.File.Length > uint.MaxValue)
                {
                    _logger.LogWarning("Client patch {path} is too large for the launcher, skipping.", clientPatch.Path);
                    continue;
                }

                var names = clientPatch.Path.Split('/');

                var folder = rootFolder;

                foreach (var folderName in names[..^1])
                    folder = GetOrAddFolder(folder, folderName);

                var file = folder.Elements("File").FirstOrDefault(x => HasName(x, names[^1]));

                if (file is null)
                {
                    file = new XElement("File", new XAttribute("name", names[^1]));
                    folder.Add(file);
                }

                var xxHash64 = new XxHash64();

                await using (var stream = clientPatch.File.OpenRead())
                    await xxHash64.AppendAsync(stream, cancellationToken);

                file.SetAttributeValue("size", clientPatch.File.Length);
                file.SetAttributeValue("hash", xxHash64.GetCurrentHashAsUInt64());

                patched++;
            }

            _manifest = new PatchedManifest(key, ToBytes(document));

            _logger.LogInformation("Built client manifest with {patched} client patch(es).", patched);

            return _manifest;
        }
        finally
        {
            _manifestLock.Release();
        }
    }

    /// <summary>
    /// The sub folder of <paramref name="parent"/> with this name (any case), added if it's missing.
    /// </summary>
    private static XElement GetOrAddFolder(XElement parent, string name)
    {
        var folder = parent.Elements("Folder").FirstOrDefault(x => HasName(x, name));

        if (folder is not null)
            return folder;

        folder = new XElement("Folder", new XAttribute("name", name));

        // The launcher's schema lists a folder's sub folders before its files.
        if (parent.Element("File") is { } firstFile)
            firstFile.AddBeforeSelf(folder);
        else
            parent.Add(folder);

        return folder;
    }

    private static bool HasName(XElement element, string name) =>
        string.Equals((string?)element.Attribute("name"), name, StringComparison.OrdinalIgnoreCase);

    private static byte[] ToBytes(XDocument document)
    {
        using var stream = new MemoryStream();

        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true }))
            document.Save(writer);

        return stream.ToArray();
    }
}
