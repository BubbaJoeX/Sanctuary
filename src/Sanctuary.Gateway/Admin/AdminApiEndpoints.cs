using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;

namespace Sanctuary.Gateway.Admin;

/// <summary>
/// The admin API's routes. Every request carries the shared key as a bearer token; all but sign-in also name the
/// referee acting (<see cref="UserHeader"/>), whose account is checked on each request. Changes are written to the
/// audit log like the chat commands that make the same changes.
/// </summary>
public static partial class AdminApiEndpoints
{
    /// <summary>
    /// The user id of the referee the admin panel is acting for.
    /// </summary>
    public const string UserHeader = "X-Sanctuary-User";

    private const int MaxMessageLength = 500;
    private const float MaxTimeScale = 100f;

    private static readonly DateTimeOffset StartedAt = DateTimeOffset.UtcNow;

    // Commands run one at a time, so each one's replies are told apart.
    private static readonly SemaphoreSlim CommandLock = new(1, 1);

    private static ILogger _logger = null!;
    private static ILogger _auditLogger = null!;

    public static void MapAdminApi(this WebApplication app)
    {
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();

        _logger = loggerFactory.CreateLogger(nameof(AdminApiEndpoints));
        _auditLogger = loggerFactory.CreateLogger("Audit");

        var api = app.MapGroup("/api").AddEndpointFilter(async (context, next) =>
        {
            var key = context.HttpContext.RequestServices.GetRequiredService<AdminApiKey>();
            var authorization = context.HttpContext.Request.Headers.Authorization.ToString();

            if (!authorization.StartsWith("Bearer ", StringComparison.Ordinal) || !key.Matches(authorization["Bearer ".Length..]))
                return Results.Unauthorized();

            return await next(context);
        });

        api.MapPost("/login", LoginHandler);

        var referee = api.MapGroup("").AddEndpointFilter(async (context, next) =>
        {
            var directory = context.HttpContext.RequestServices.GetRequiredService<RefereeDirectory>();

            if (!ulong.TryParse(context.HttpContext.Request.Headers[UserHeader], out var userId)
                || directory.Find(userId) is not { } found)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            context.HttpContext.Items[typeof(Referee)] = found;

            return await next(context);
        });

        referee.MapGet("/me", (HttpContext context, RefereeDirectory directory) => ToModel(GetReferee(context), directory));

        referee.MapGet("/status", StatusHandler);
        referee.MapPut("/settings", SettingsHandler);
        referee.MapPost("/broadcast", BroadcastHandler);
        referee.MapPut("/events/{key}", EventHandler);

        referee.MapGet("/resources", (IResourceManager resourceManager) =>
            resourceManager.ReloadableResources.Order().Select(x => new ResourceModel(x)));
        referee.MapPost("/resources/{name}/reload", ReloadHandler);

        referee.MapGet("/commands", CommandsHandler);
        referee.MapPost("/command", CommandHandlerAsync);

        referee.MapGet("/players", PlayersHandler);
        referee.MapPost("/players/{id}/teleport", TeleportHandler);
        referee.MapPost("/players/{id}/message", MessageHandler);
        referee.MapPost("/players/{id}/kick", KickHandler);

        MapZoneEndpoints(referee);
    }

    #region Referees

    private static IResult LoginHandler(LoginRequest request, RefereeDirectory directory)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
            return Results.BadRequest("Enter a username and password.");

        if (directory.SignIn(request.Username, request.Password) is not { } referee)
        {
            _logger.LogWarning("Admin panel sign-in failed for {Username}.", request.Username);
            return Results.Unauthorized();
        }

        _logger.LogInformation("{Username} signed in to the admin panel.", referee.Name);

        return Results.Ok(ToModel(referee, directory));
    }

    private static Referee GetReferee(HttpContext context) => (Referee)context.Items[typeof(Referee)]!;

    private static RefereeModel ToModel(Referee referee, RefereeDirectory directory) =>
        new(referee.Id.ToString(CultureInfo.InvariantCulture), referee.Name, referee.Role.ToString(),
            directory.FindOnlineCharacter(referee)?.Name.FullName);

    private static void Audit(Referee referee, string action, string? target, string? detail)
    {
        _auditLogger.LogInformation("panel|{Action}|Actor: \"{ActorName}\" (user {ActorId}){Target}{Detail}",
            action,
            referee.Name,
            referee.Id,
            target is null ? string.Empty : $", Target: \"{target}\"",
            detail is null ? string.Empty : $", {detail}");
    }

    #endregion

    #region Server

    private static StatusModel StatusHandler(IZoneManager zoneManager, IServerSettings serverSettings, IWorldEventManager worldEventManager)
    {
        var zones = zoneManager.Zones.ToList();

        return new StatusModel(
            StartedAt,
            zones.Sum(x => x.Players.Count()),
            zones.Count,
            serverSettings.IsLocked,
            serverSettings.MotdTitle,
            serverSettings.MotdMessage,
            serverSettings.GameTimeScalar,
            worldEventManager.GetAll().OrderBy(x => x.Key).Select(x => new EventModel(x.Key, x.Value)).ToList());
    }

    /// <summary>
    /// Locks or unlocks the world, sets the message of the day, or changes the game clock's speed, with the same
    /// effect on players online as !lock, !motd and !timescale.
    /// </summary>
    private static IResult SettingsHandler(HttpContext context, SettingsRequest request, IZoneManager zoneManager, IServerSettings serverSettings,
        IWorldEventManager worldEventManager)
    {
        var referee = GetReferee(context);

        if (request.TimeScale is { } timeScale && (timeScale <= 0f || timeScale > MaxTimeScale))
            return Results.BadRequest($"The time scale must be above 0 and at most {MaxTimeScale}.");

        if (request.MotdMessage is { Length: > MaxMessageLength } || request.MotdTitle is { Length: > 100 })
            return Results.BadRequest("That message of the day is too long.");

        var players = zoneManager.Zones.SelectMany(zone => zone.Players).ToList();

        if (request.Locked is { } locked && locked != serverSettings.IsLocked)
        {
            serverSettings.IsLocked = locked;

            var kicked = 0;

            if (locked && request.KickPlayers == true)
            {
                foreach (var player in players.Where(player => !player.IsMod && !player.IsAdmin))
                {
                    ChatHelper.SendSystemMessage(player, "The server has been locked for maintenance.");
                    player.Disconnect();
                    kicked++;
                }
            }

            Audit(referee, locked ? "Lock" : "Unlock", null, kicked > 0 ? $"Kicked: {kicked}" : null);
        }

        if (request.MotdTitle is not null || request.MotdMessage is not null)
        {
            serverSettings.MotdTitle = string.IsNullOrWhiteSpace(request.MotdTitle) ? null : request.MotdTitle.Trim();
            serverSettings.MotdMessage = string.IsNullOrWhiteSpace(request.MotdMessage) ? null : request.MotdMessage.Trim();

            if (serverSettings.CreateMotd() is { } packetMotd)
            {
                foreach (var player in players)
                    player.SendTunneled(packetMotd);
            }

            Audit(referee, "Motd", null, $"Title: \"{serverSettings.MotdTitle}\", Message: \"{serverSettings.MotdMessage}\"");
        }

        if (request.TimeScale is { } scalar && scalar != serverSettings.GameTimeScalar)
        {
            serverSettings.GameTimeScalar = scalar;

            var packetClientGameSettings = serverSettings.CreateClientGameSettings();

            foreach (var player in players)
                player.SendTunneled(packetClientGameSettings);

            Audit(referee, "TimeScale", null, $"Scalar: {scalar}");
        }

        return Results.Ok(StatusHandler(zoneManager, serverSettings, worldEventManager));
    }

    private static IResult BroadcastHandler(HttpContext context, BroadcastRequest request, IZoneManager zoneManager)
    {
        var message = request.Message?.Trim();

        if (string.IsNullOrEmpty(message) || message.Length > MaxMessageLength)
            return Results.BadRequest($"Write a message of up to {MaxMessageLength} characters.");

        IZone? onlyZone = null;

        if (request.Zone is not null && !TryGetZone(zoneManager, request.Zone, out onlyZone))
            return Results.NotFound("That zone isn't running.");

        var recipients = onlyZone is not null
            ? onlyZone.Players.ToList()
            : zoneManager.Zones.SelectMany(zone => zone.Players).ToList();

        var bannerPacket = request.Banner ? new BroadcastPacketZone { Message = message } : null;

        foreach (var player in recipients)
        {
            ChatHelper.SendSystemMessage(player, $"[Broadcast] {message}");

            if (bannerPacket is not null)
                player.SendTunneled(bannerPacket);
        }

        Audit(GetReferee(context), onlyZone is null ? "Broadcast" : "ZoneBroadcast", onlyZone?.Name,
            $"Recipients: {recipients.Count}, Banner: {request.Banner}, Message: \"{message}\"");

        return Results.Ok(new { recipients = recipients.Count });
    }

    private static IResult EventHandler(HttpContext context, string key, EventRequest request, IWorldEventManager worldEventManager)
    {
        key = key.Trim().ToLowerInvariant();

        if (key.Length is 0 or > 32)
            return Results.BadRequest("Event keys are 1-32 characters.");

        worldEventManager.SetActive(key, request.Active);

        Audit(GetReferee(context), request.Active ? "EventOn" : "EventOff", null, $"Event: {key}");

        return Results.Ok(new EventModel(key, worldEventManager.IsActive(key)));
    }

    private static IResult ReloadHandler(HttpContext context, string name, IResourceManager resourceManager)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        if (!resourceManager.ReloadableResources.Contains(name, StringComparer.OrdinalIgnoreCase))
            return Results.NotFound($"{name} can't be reloaded.");

        if (!resourceManager.TryReload(name))
            return Results.Problem($"Reloading {name} failed; see the gateway log.");

        Audit(referee, "Reload", null, $"Resource: {name}");

        return Results.NoContent();
    }

    #endregion

    #region Console

    private static IResult CommandsHandler(HttpContext context, IChatCommandManager chatCommandManager)
    {
        var referee = GetReferee(context);

        return Results.Ok(chatCommandManager.Commands
            .Where(x => x.RequiredRole <= referee.Role)
            .OrderBy(x => x.KeyWord)
            .Select(x => new CommandModel(x.KeyWord, x.Usage, x.Description, x.RequiredRole.ToString())));
    }

    /// <summary>
    /// Runs a chat command as the referee's character in the world, as if they'd typed it, and answers with what
    /// it said back to them.
    /// </summary>
    private static async Task<IResult> CommandHandlerAsync(HttpContext context, CommandRequest request, RefereeDirectory directory,
        IChatCommandManager chatCommandManager)
    {
        var referee = GetReferee(context);
        var command = request.Command?.Trim().TrimStart('!') ?? string.Empty;

        if (command.Length == 0)
            return Results.BadRequest("Type a command.");

        if (directory.FindOnlineCharacter(referee) is not { } player)
            return Results.Conflict("Commands run as your character, and none of yours is in the world.");

        var replies = new ConcurrentQueue<string>();
        bool handled;

        await CommandLock.WaitAsync(context.RequestAborted);

        try
        {
            player.SystemMessageCapture = replies;

            handled = chatCommandManager.TryHandle(player, chatCommandManager.Prefix + command);

            // Some commands answer a moment later (database work, effects); give them a beat.
            await Task.Delay(250, CancellationToken.None);
        }
        finally
        {
            player.SystemMessageCapture = null;
            CommandLock.Release();
        }

        _logger.LogInformation("{Username} ran \"{Command}\" as {Character} from the admin panel.", referee.Name, command, player.Name.FullName);

        var lines = replies.Where(x => !x.StartsWith(ChatHelper.GmPanelToken, StringComparison.Ordinal)).ToList();

        if (!handled)
            lines.Add($"There's no {chatCommandManager.Prefix}{command.Split(' ')[0]} command.");

        return Results.Ok(new CommandResult(player.Name.FullName, handled, lines));
    }

    #endregion

    #region Players

    private static PlayerModel[] PlayersHandler(IZoneManager zoneManager) => zoneManager.Zones
        .SelectMany(zone => zone.Players)
        .OrderBy(x => x.Name.FullName)
        .Select(player => new PlayerModel(
            Id(player.Guid),
            player.Name.FullName,
            ZoneKey(player.Zone),
            player.Zone.Name,
            Round(player.Position.X),
            Round(player.Position.Y),
            Round(player.Position.Z),
            Round(NpcProperties.GetHeading(player.Rotation)),
            player.ChatCommandRole.ToString(),
            player.GodMode,
            player.FrozenMovementSpeed is not null,
            player.MutedUntil > DateTimeOffset.UtcNow))
        .ToArray();

    /// <summary>
    /// Sends a player to a spot (in their zone, or another world zone), to a zone's spawn point, or to another player.
    /// </summary>
    private static IResult TeleportHandler(HttpContext context, string id, TeleportRequest request, IZoneManager zoneManager,
        IResourceManager resourceManager)
    {
        if (!TryGetOnlinePlayer(zoneManager, id, out var target))
            return Results.NotFound("That player isn't online.");

        IZone zone;
        Vector4 position;

        if (request.ToPlayer is not null)
        {
            if (!TryGetOnlinePlayer(zoneManager, request.ToPlayer, out var destination))
                return Results.NotFound("The player to send them to isn't online.");

            zone = destination.Zone;
            position = destination.Position;
        }
        else
        {
            zone = target.Zone;

            if (request.Zone is not null && !TryGetZone(zoneManager, request.Zone, out zone!))
                return Results.NotFound("That zone isn't running.");

            if (request.X is { } x && request.Z is { } z)
            {
                var y = request.Y ?? (zone.TryGetGroundHeight(x, z, 10_000f, 20_000f, out var ground) ? ground : target.Position.Y);
                position = new Vector4(x, y, z, 1f);
            }
            else
            {
                position = zone.SpawnPosition;
            }
        }

        position = position with { W = 1f };

        if (ReferenceEquals(zone, target.Zone))
        {
            target.UpdatePosition(position, target.Rotation);

            target.SendTunneled(new ClientUpdatePacketUpdateLocation
            {
                Position = position,
                Rotation = target.Rotation,
                Teleport = true
            });
        }
        else if (zone is not WorldZone || !target.TeleportToZone(zone.DefinitionId, zone.OwnerId, position, target.Rotation))
        {
            return Results.Problem($"Couldn't move {target.Name.FullName} to {zone.Name}.");
        }

        ChatHelper.SendSystemMessage(target, "You were moved by a referee.");

        Audit(GetReferee(context), "Teleport", target.Name.FullName, $"Zone: {zone.Name}, Position: {position.X:F1}, {position.Y:F1}, {position.Z:F1}");

        return Results.NoContent();
    }

    private static IResult MessageHandler(HttpContext context, string id, MessageRequest request, IZoneManager zoneManager)
    {
        var message = request.Message?.Trim();

        if (string.IsNullOrEmpty(message) || message.Length > MaxMessageLength)
            return Results.BadRequest($"Write a message of up to {MaxMessageLength} characters.");

        if (!TryGetOnlinePlayer(zoneManager, id, out var target))
            return Results.NotFound("That player isn't online.");

        var referee = GetReferee(context);

        ChatHelper.SendSystemMessage(target, $"[Referee {referee.Name}] {message}");

        Audit(referee, "Message", target.Name.FullName, $"Message: \"{message}\"");

        return Results.NoContent();
    }

    private static IResult KickHandler(HttpContext context, string id, IZoneManager zoneManager)
    {
        if (!TryGetOnlinePlayer(zoneManager, id, out var target))
            return Results.NotFound("That player isn't online.");

        var referee = GetReferee(context);

        if ((target.IsMod || target.IsAdmin) && referee.Role <= target.ChatCommandRole)
            return Results.Problem($"You can't kick {target.Name.FullName}.", statusCode: StatusCodes.Status403Forbidden);

        ChatHelper.SendSystemMessage(target, "You have been kicked from the server.");

        target.Disconnect();

        Audit(referee, "Kick", target.Name.FullName, null);

        return Results.NoContent();
    }

    private static bool TryGetOnlinePlayer(IZoneManager zoneManager, string id, out Player player)
    {
        player = null!;

        return ulong.TryParse(id, out var guid) && zoneManager.TryGetPlayer(guid, out player!);
    }

    #endregion

    #region Helpers

    private static string Id(ulong guid) => guid.ToString(CultureInfo.InvariantCulture);

    private static float Round(float value) => MathF.Round(value, 2);

    /// <summary>
    /// A running zone's key: its definition id, and for an instance (a house) its owner too.
    /// </summary>
    private static string ZoneKey(IZone zone) => zone.OwnerId is { } ownerId
        ? $"{zone.DefinitionId}.{ownerId}"
        : zone.DefinitionId.ToString(CultureInfo.InvariantCulture);

    private static bool TryGetZone(IZoneManager zoneManager, string key, out IZone zone)
    {
        zone = zoneManager.Zones.FirstOrDefault(x => ZoneKey(x) == key)!;
        return zone is not null;
    }

    #endregion
}
