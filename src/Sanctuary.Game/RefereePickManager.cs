using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game;

public interface IRefereePickManager
{
    /// <summary>
    /// The picks on the welcome screen, in order.
    /// </summary>
    IReadOnlyList<WelcomeCard> Current { get; }

    /// <summary>
    /// Adds a pick at the end. False when the welcome screen is full.
    /// </summary>
    bool TryAdd(WelcomeCard pick, ulong? updatedBy);

    bool TryRemoveAt(int index);

    void Clear();

    /// <summary>
    /// Restores the stock picks in Resources/RefereePicks.json.
    /// </summary>
    void Reset(ulong? updatedBy);

    /// <summary>
    /// Reads the picks again and, when they changed, sends them to everyone online. True when they changed.
    /// </summary>
    bool Refresh();

    /// <summary>
    /// Refreshes every <see cref="RefereePickHelper.PollInterval"/>, so picks edited on the admin site reach players.
    /// </summary>
    Task WatchAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The welcome screen's Referee Picks, kept in the RefereePicks table. Referees edit them with !pick, or on the
/// WebAPI's admin site, which shares the table; the Gateway polls it to pick those edits up.
/// </summary>
public class RefereePickManager : IRefereePickManager
{
    public static readonly string DefaultsFile = Path.Combine(ResourceManager.BaseDirectory, "RefereePicks.json");

    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IResourceManager _resourceManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    private readonly object _lock = new();
    private IReadOnlyList<WelcomeCard>? _current;

    public RefereePickManager(
        ILogger<RefereePickManager> logger,
        IServiceProvider serviceProvider,
        IResourceManager resourceManager,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _resourceManager = resourceManager;
        _dbContextFactory = dbContextFactory;
    }

    public IReadOnlyList<WelcomeCard> Current
    {
        get
        {
            lock (_lock)
                return _current ??= Read();
        }
    }

    public bool TryAdd(WelcomeCard pick, ulong? updatedBy)
    {
        using (var dbContext = _dbContextFactory.CreateDbContext())
        {
            var picks = RefereePickHelper.Load(dbContext);

            if (picks.Count >= RefereePickHelper.MaxPicks)
                return false;

            dbContext.RefereePicks.Add(new DbRefereePick
            {
                Position = picks.Count == 0 ? 0 : picks[^1].Position + 1,
                Title = pick.Title,
                Text = pick.Text,
                Action = pick.Action,
                Param1 = pick.Param1,
                Icon = pick.Icon,
                Button = pick.Button,
                UpdatedBy = updatedBy
            });

            dbContext.SaveChanges();
        }

        Refresh();
        return true;
    }

    public bool TryRemoveAt(int index)
    {
        using (var dbContext = _dbContextFactory.CreateDbContext())
        {
            var picks = RefereePickHelper.Load(dbContext);

            if (index < 0 || index >= picks.Count)
                return false;

            var id = picks[index].Id;

            dbContext.RefereePicks.Where(x => x.Id == id).ExecuteDelete();
        }

        Refresh();
        return true;
    }

    public void Clear()
    {
        using (var dbContext = _dbContextFactory.CreateDbContext())
            dbContext.RefereePicks.ExecuteDelete();

        Refresh();
    }

    public void Reset(ulong? updatedBy)
    {
        var defaults = RefereePickHelper.LoadDefaults(DefaultsFile, _resourceManager.PointOfInterests.ContainsKey);

        using (var dbContext = _dbContextFactory.CreateDbContext())
            RefereePickHelper.Replace(dbContext, defaults, updatedBy);

        Refresh();
    }

    public bool Refresh()
    {
        List<WelcomeCard> picks;

        lock (_lock)
        {
            picks = Read();

            if (_current is not null && _current.SequenceEqual(picks))
                return false;

            _current = picks;
        }

        // The client keeps the announcements it was sent and fills the welcome screen from them when it opens.
        // Resolved lazily: zones resolve this manager while the zone manager is still loading.
        var zoneManager = _serviceProvider.GetRequiredService<IZoneManager>();
        var packet = WelcomeScreenHelper.GetAnnouncementsPacket(picks);

        foreach (var player in zoneManager.Zones.SelectMany(zone => zone.Players))
            player.SendTunneled(packet);

        return true;
    }

    public async Task WatchAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(RefereePickHelper.PollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    if (Refresh())
                        _logger.LogInformation("Referee Picks changed, sent them to everyone online.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refresh the Referee Picks.");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private List<WelcomeCard> Read()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return RefereePickHelper.Load(dbContext)
            .Select(x => new WelcomeCard(x.Title, x.Text, x.Action, x.Param1, x.Icon, x.Button))
            .Where(card => WelcomeCard.Actions.ContainsKey(card.Action))
            .Take(RefereePickHelper.MaxPicks)
            .ToList();
    }
}
