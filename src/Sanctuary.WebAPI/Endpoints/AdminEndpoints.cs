using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using MiniValidation;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.WebAPI.Models;

using BC = BCrypt.Net.BCrypt;

namespace Sanctuary.WebAPI.Endpoints;

/// <summary>
/// The admin site at /admin, where referees (admin and mod accounts) sign in and edit the welcome screen's
/// Referee Picks. The Gateway polls the same table, so saves reach players online without a restart.
/// </summary>
public static class AdminEndpoints
{
    private const string RefereePolicy = "Referee";
    private const string LoginRateLimitPolicy = "AdminLogin";

    // Sent by the admin page on every change. Browsers won't add a custom header to a cross-site request
    // without a CORS preflight, which this API never allows, so a change can't be forged from another site.
    private const string RequestHeader = "X-Sanctuary-Admin";

    private static readonly string PageFile = Path.Combine(AppContext.BaseDirectory, "Admin", "index.html");
    private static readonly string DefaultPicksFile = Path.Combine(AppContext.BaseDirectory, "Resources", "RefereePicks.json");
    private static readonly string PointOfInterestsFile = Path.Combine(AppContext.BaseDirectory, "Resources", "PointOfInterests.json");

    // Checked when there's no such user, so a wrong username takes as long to refuse as a wrong password.
    private static readonly string UnknownUserHash = BC.HashPassword(Guid.NewGuid().ToString("N"));

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<IReadOnlyDictionary<int, string>> PointsOfInterest = new(LoadPointsOfInterest);

    private static ILogger _logger = null!;

    public static void AddAdminEndpointServices(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "sanctuary_admin";
                options.Cookie.Path = "/admin";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;

                // The admin page calls a JSON API, so answer with status codes rather than redirects.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };

                options.Events.OnValidatePrincipal = ValidatePrincipalAsync;
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(RefereePolicy, policy => policy.RequireAuthenticatedUser());

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(LoginRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
        });
    }

    public static void MapAdminEndpoints(this WebApplication app)
    {
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();

        _logger = loggerFactory.CreateLogger(nameof(AdminEndpoints));

        app.MapGet("/admin", (HttpContext context) =>
        {
            // Revalidated on every visit, so browsers don't keep showing an older build's page.
            context.Response.Headers.CacheControl = "no-cache";

            return Results.File(PageFile, "text/html; charset=utf-8");
        });

        var api = app.MapGroup("/admin/api")
            .AddEndpointFilter(async (context, next) =>
            {
                var request = context.HttpContext.Request;

                if (!HttpMethods.IsGet(request.Method) && !request.Headers.ContainsKey(RequestHeader))
                    return Results.BadRequest($"Missing the {RequestHeader} header.");

                return await next(context);
            });

        api.MapPost("/login", LoginHandlerAsync).RequireRateLimiting(LoginRateLimitPolicy);
        api.MapPost("/logout", LogoutHandlerAsync);

        var referee = api.MapGroup("").RequireAuthorization(RefereePolicy);

        referee.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(GetAdminUser(user)));
        referee.MapGet("/picks", GetPicksHandler);
        referee.MapPut("/picks", SavePicksHandler);
        referee.MapPost("/picks/reset", ResetPicksHandler);
    }

    private static async Task<IResult> LoginHandlerAsync(
        HttpContext context,
        LoginRequestModel request,
        CancellationToken cancellationToken,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        if (!MiniValidator.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var dbUser = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Username == request.Username, cancellationToken);

        var passwordMatches = BC.Verify(request.Password, dbUser?.Password ?? UnknownUserHash);

        if (dbUser is null || !passwordMatches || !IsReferee(dbUser))
        {
            _logger.LogWarning("Admin login failed for username: {Username}", request.Username);

            return Results.Unauthorized();
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, dbUser.Id.ToString()),
            new Claim(ClaimTypes.Name, dbUser.Username),
            new Claim(ClaimTypes.Role, dbUser.IsAdmin ? "Admin" : "Mod")
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        _logger.LogInformation("{Username} signed in to the admin site.", dbUser.Username);

        return Results.Ok(GetAdminUser(principal));
    }

    private static async Task<IResult> LogoutHandlerAsync(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return Results.NoContent();
    }

    private static IResult GetPicksHandler(IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return Results.Ok(GetPicks(dbContext));
    }

    private static IResult SavePicksHandler(
        ClaimsPrincipal user,
        RefereePicksSaveModel request,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        if (request.Picks.Count > RefereePickHelper.MaxPicks)
            return Results.BadRequest($"The welcome screen holds {RefereePickHelper.MaxPicks} picks.");

        var picks = request.Picks.Select(x => new DbRefereePick
        {
            Title = x.Title.Trim(),
            Text = x.Text.Trim(),
            Action = x.Action,
            Param1 = x.Param1,
            Icon = x.Icon.Trim(),
            Button = x.Button.Trim()
        }).ToList();

        for (var i = 0; i < picks.Count; i++)
        {
            if (RefereePickHelper.Validate(picks[i], PointsOfInterest.Value.ContainsKey) is { } error)
                return Results.BadRequest($"Pick {i + 1}: {error}");
        }

        using var dbContext = dbContextFactory.CreateDbContext();

        if (GetVersion(RefereePickHelper.Load(dbContext)) != request.Version)
            return Results.Conflict("Someone else changed the picks since you loaded them. Reload to see their changes.");

        RefereePickHelper.Replace(dbContext, picks, null);

        _logger.LogInformation("{Username} saved {Count} Referee Picks on the admin site.", user.Identity?.Name, picks.Count);

        return Results.Ok(GetPicks(dbContext));
    }

    private static IResult ResetPicksHandler(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        var defaults = RefereePickHelper.LoadDefaults(DefaultPicksFile, PointsOfInterest.Value.ContainsKey);

        using var dbContext = dbContextFactory.CreateDbContext();

        RefereePickHelper.Replace(dbContext, defaults, null);

        _logger.LogInformation("{Username} restored the stock Referee Picks on the admin site.", user.Identity?.Name);

        return Results.Ok(GetPicks(dbContext));
    }

    private static RefereePicksModel GetPicks(DatabaseContext dbContext)
    {
        var picks = RefereePickHelper.Load(dbContext);

        return new RefereePicksModel
        {
            Version = GetVersion(picks),
            Picks = picks.Select(x => new RefereePickModel
            {
                Title = x.Title,
                Text = x.Text,
                Action = x.Action,
                Param1 = x.Param1,
                Icon = x.Icon,
                Button = x.Button
            }).ToList(),
            MaxPicks = RefereePickHelper.MaxPicks,
            MaxTitleLength = RefereePickHelper.MaxTitleLength,
            MaxTextLength = RefereePickHelper.MaxTextLength,
            MaxButtonLength = RefereePickHelper.MaxButtonLength,
            RefreshSeconds = (int)RefereePickHelper.PollInterval.TotalSeconds,
            Kinds = RefereePickHelper.Kinds.Select(x => new RefereePickKindModel(x.Key, x.Value.Action, x.Value.Icon, x.Value.Button)).ToList(),
            Icons = RefereePickHelper.Icons,
            PointsOfInterest = PointsOfInterest.Value.Select(x => new PointOfInterestModel(x.Key, x.Value)).ToList()
        };
    }

    /// <summary>
    /// A hash of the picks' content: a save is refused when the picks changed since the editor loaded them.
    /// </summary>
    private static string GetVersion(List<DbRefereePick> picks)
    {
        var content = JsonSerializer.SerializeToUtf8Bytes(picks.Select(x => new { x.Title, x.Text, x.Action, x.Param1, x.Icon, x.Button }));

        return Convert.ToHexString(SHA256.HashData(content))[..16];
    }

    private static bool IsReferee(DbUser dbUser) =>
        (dbUser.IsAdmin || dbUser.IsMod) && !(dbUser.LockedUntil > DateTimeOffset.UtcNow);

    private static AdminUserModel GetAdminUser(ClaimsPrincipal user) =>
        new(user.Identity?.Name ?? "", user.FindFirstValue(ClaimTypes.Role) ?? "");

    /// <summary>
    /// Signs a referee out once their account loses its admin or mod flag, or is locked.
    /// </summary>
    private static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        if (!ulong.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            context.RejectPrincipal();
            return;
        }

        var dbContextFactory = context.HttpContext.RequestServices.GetRequiredService<IDbContextFactory<DatabaseContext>>();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(context.HttpContext.RequestAborted);

        var dbUser = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, context.HttpContext.RequestAborted);

        if (dbUser is null || !IsReferee(dbUser))
        {
            context.RejectPrincipal();

            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    /// <summary>
    /// Points of interest a teleport pick can send players to, by id, named as in Resources/PointOfInterests.json.
    /// </summary>
    private static IReadOnlyDictionary<int, string> LoadPointsOfInterest()
    {
        if (!File.Exists(PointOfInterestsFile))
        {
            _logger.LogError("Failed to find file \"{file}\"", PointOfInterestsFile);
            return new Dictionary<int, string>();
        }

        using var stream = File.OpenRead(PointOfInterestsFile);

        return (JsonSerializer.Deserialize<List<PointOfInterestEntry>>(stream, JsonOptions) ?? [])
            .GroupBy(x => x.Id)
            .ToDictionary(x => x.Key, x => x.First().Comment ?? $"Point of interest {x.Key}");
    }

    private sealed record PointOfInterestEntry(int Id, string? Comment);
}
