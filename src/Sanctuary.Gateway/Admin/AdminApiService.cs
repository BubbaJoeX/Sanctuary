using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Authoring;

namespace Sanctuary.Gateway.Admin;

/// <summary>
/// Runs the admin API: a small HTTP app inside the gateway that the admin panel's backend calls to see and change
/// the live game. It shares the game's zones and managers, so an edit made through it reaches players in view at
/// once, exactly as the same edit made with a chat command would.
/// </summary>
public sealed class AdminApiService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly AdminApiOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _gameServices;

    public AdminApiService(
        ILogger<AdminApiService> logger,
        IOptions<AdminApiOptions> options,
        ILoggerFactory loggerFactory,
        IServiceProvider gameServices)
    {
        _logger = logger;
        _options = options.Value;
        _loggerFactory = loggerFactory;
        _gameServices = gameServices;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(_options.Key))
        {
            _logger.LogInformation("The admin API is off; set {Section}:Key to let the admin panel in.", AdminApiOptions.Section);
            return;
        }

        if (_options.Key.Length < AdminApiOptions.MinKeyLength)
        {
            _logger.LogError("The admin API is off: {Section}:Key must be at least {Length} characters.",
                AdminApiOptions.Section, AdminApiOptions.MinKeyLength);
            return;
        }

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.WebHost.UseUrls(_options.Urls);

        // Logs go wherever the gateway's do.
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(_loggerFactory);

        builder.Services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        // The game's own instances, so the endpoints work on the live world.
        builder.Services.AddSingleton(_gameServices.GetRequiredService<IZoneManager>());
        builder.Services.AddSingleton(_gameServices.GetRequiredService<IResourceManager>());
        builder.Services.AddSingleton(_gameServices.GetRequiredService<IHousingManager>());
        builder.Services.AddSingleton(_gameServices.GetRequiredService<IServerSettings>());
        builder.Services.AddSingleton(_gameServices.GetRequiredService<IWorldEventManager>());
        builder.Services.AddSingleton(_gameServices.GetRequiredService<IChatCommandManager>());
        builder.Services.AddSingleton(_gameServices.GetRequiredService<IRewardManager>());
        builder.Services.AddSingleton(_gameServices.GetRequiredService<ICustomQuestManager>());
        builder.Services.AddSingleton(_gameServices.GetRequiredService<IDbContextFactory<DatabaseContext>>());
        builder.Services.AddSingleton(_gameServices.GetRequiredService<IScriptLibrary>());

        builder.Services.AddSingleton(new AdminApiKey(_options.Key));
        builder.Services.AddSingleton<RefereeDirectory>();
        builder.Services.AddSingleton<AdminUndoBook>();
        builder.Services.AddSingleton<ZoneHeightMaps>();

        var app = builder.Build();

        app.MapAdminApi();

        try
        {
            await app.StartAsync(stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "The admin API failed to start on {Urls}.", _options.Urls);
            return;
        }

        _logger.LogInformation("Admin API listening on {Urls}.", _options.Urls);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }

        await app.StopAsync(CancellationToken.None);
        await app.DisposeAsync();
    }
}
