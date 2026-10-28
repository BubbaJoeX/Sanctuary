using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game;

public class WorldEventManager : IWorldEventManager
{
    /// <summary>
    /// How often seasons are checked for starting or ending.
    /// </summary>
    private static readonly TimeSpan SeasonCheckInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceProvider _serviceProvider;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    private readonly Lazy<ConcurrentDictionary<string, bool>> _events;

    /// <summary>
    /// Whether each season was on at the last check, to notice it starting or ending.
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _seasons = new(StringComparer.OrdinalIgnoreCase);

    private readonly Timer _seasonTimer;

    public WorldEventManager(IServiceProvider serviceProvider, IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _serviceProvider = serviceProvider;
        _dbContextFactory = dbContextFactory;

        _events = new(LoadEvents);

        _seasonTimer = new Timer(_ => CheckSeasons(), null, SeasonCheckInterval, SeasonCheckInterval);
    }

    private ConcurrentDictionary<string, bool> LoadEvents()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return new(dbContext.WorldEvents
            .AsNoTracking()
            .ToDictionary(x => x.Key, x => x.IsActive), StringComparer.OrdinalIgnoreCase);
    }

    // Resolved lazily: zones resolve this manager while the zone manager and resources are still loading.
    private IResourceManager Resources => _serviceProvider.GetRequiredService<IResourceManager>();

    /// <summary>
    /// On while !event has it on, or (a season, Resources/Seasons.json) between its days of the year.
    /// </summary>
    public bool IsActive(string key) => _events.Value.TryGetValue(key, out var active) && active || IsInSeason(key);

    private bool IsInSeason(string key) => Resources.Seasons.TryGetValue(key.ToLowerInvariant(), out var season) && season.IsActive(DateTime.Now);

    public IReadOnlyDictionary<string, bool> GetAll()
    {
        var all = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in _events.Value.Keys.Concat(Resources.Seasons.Keys))
            all[key] = IsActive(key);

        return all;
    }

    public EventJoinBook Joins { get; } = new();

    public void SetActive(string key, bool active)
    {
        using (var dbContext = _dbContextFactory.CreateDbContext())
        {
            var dbEvent = dbContext.WorldEvents.SingleOrDefault(x => x.Key == key);

            if (dbEvent is null)
                dbContext.WorldEvents.Add(new DbWorldEvent { Key = key, IsActive = active });
            else
                dbEvent.IsActive = active;

            dbContext.SaveChanges();
        }

        _events.Value[key] = active;

        // A season stays on through its days whatever !event says.
        var now = IsActive(key);

        if (_seasons.ContainsKey(key))
            _seasons[key] = now;

        Notify(key, now);
    }

    /// <summary>
    /// Spawns or despawns the event's NPCs in every loaded world zone, and tells the zones' scripts.
    /// </summary>
    private void Notify(string key, bool active)
    {
        var zoneManager = _serviceProvider.GetRequiredService<IZoneManager>();

        foreach (var zone in zoneManager.Zones.OfType<WorldZone>())
        {
            zone.OnWorldEventChanged(key, active);

            zone.GetOrCreateScriptContext().FireEvent(active ? "eventStart" : "eventEnd", key);
        }
    }

    /// <summary>
    /// Starts or ends the seasons whose days began or ran out since the last check. The first check only notes them:
    /// zones spawn what's on as they load.
    /// </summary>
    private void CheckSeasons()
    {
        try
        {
            foreach (var key in Resources.Seasons.Keys)
            {
                var now = IsActive(key);

                if (_seasons.TryGetValue(key, out var before) && before != now)
                    Notify(key, now);

                _seasons[key] = now;
            }
        }
        catch (InvalidOperationException)
        {
            // The zones or resources aren't there yet; the next check catches up.
        }
    }
}
