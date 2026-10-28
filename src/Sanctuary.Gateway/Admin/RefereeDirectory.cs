using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game;
using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Entities;

using BC = BCrypt.Net.BCrypt;

namespace Sanctuary.Gateway.Admin;

/// <summary>
/// A referee (admin or mod account) acting through the admin panel.
/// </summary>
public sealed record Referee(ulong Id, string Name, ChatCommandRole Role, ulong[] CharacterIds)
{
    public bool IsAdmin => Role >= ChatCommandRole.Admin;
}

/// <summary>
/// The shared secret the admin panel's backend proves itself with.
/// </summary>
public sealed class AdminApiKey
{
    private readonly byte[] _key;

    public AdminApiKey(string key)
    {
        _key = Encoding.UTF8.GetBytes(key);
    }

    public bool Matches(string? candidate) =>
        candidate is not null && CryptographicOperations.FixedTimeEquals(_key, Encoding.UTF8.GetBytes(candidate));
}

/// <summary>
/// Signs referees in with their game accounts and looks them up for each request. Lookups are cached briefly,
/// so an account that loses its admin or mod flag, or is locked, is refused within <see cref="CacheFor"/>.
/// </summary>
public sealed class RefereeDirectory
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(10);

    // Checked when there's no such user, so a wrong username takes as long to refuse as a wrong password.
    private static readonly string UnknownUserHash = BC.HashPassword(Guid.NewGuid().ToString("N"));

    private readonly IZoneManager _zoneManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly ConcurrentDictionary<ulong, (Referee? Referee, DateTimeOffset Fetched)> _cache = new();

    public RefereeDirectory(IZoneManager zoneManager, IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _zoneManager = zoneManager;
        _dbContextFactory = dbContextFactory;
    }

    public Referee? SignIn(string username, string password)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbUser = dbContext.Users.AsNoTracking().FirstOrDefault(x => x.Username == username);

        var passwordMatches = BC.Verify(password, dbUser?.Password ?? UnknownUserHash);

        if (dbUser is null || !passwordMatches)
            return null;

        var referee = ToReferee(dbContext, dbUser);

        _cache[dbUser.Id] = (referee, DateTimeOffset.UtcNow);

        return referee;
    }

    public Referee? Find(ulong userId)
    {
        if (_cache.TryGetValue(userId, out var cached) && DateTimeOffset.UtcNow - cached.Fetched < CacheFor)
            return cached.Referee;

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbUser = dbContext.Users.AsNoTracking().FirstOrDefault(x => x.Id == userId);

        var referee = dbUser is null ? null : ToReferee(dbContext, dbUser);

        _cache[userId] = (referee, DateTimeOffset.UtcNow);

        return referee;
    }

    /// <summary>
    /// The referee's character that is in the world right now, if any.
    /// </summary>
    public Player? FindOnlineCharacter(Referee referee) => _zoneManager.Zones
        .SelectMany(zone => zone.Players)
        .FirstOrDefault(player => referee.CharacterIds.Contains(player.CharacterId));

    private static Referee? ToReferee(DatabaseContext dbContext, DbUser dbUser)
    {
        if (!(dbUser.IsAdmin || dbUser.IsMod) || dbUser.LockedUntil > DateTimeOffset.UtcNow)
            return null;

        var characterIds = dbContext.Characters
            .AsNoTracking()
            .Where(x => x.UserId == dbUser.Id)
            .Select(x => x.Id)
            .ToArray();

        return new Referee(dbUser.Id, dbUser.Username, dbUser.IsAdmin ? ChatCommandRole.Admin : ChatCommandRole.Mod, characterIds);
    }
}
