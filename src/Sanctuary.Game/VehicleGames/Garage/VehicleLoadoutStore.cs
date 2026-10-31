using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.VehicleGames.Garage;

/// <summary>
/// A character's kart garage loadouts in the database, and in their ClientPcData (the client's loadout list).
/// </summary>
public static class VehicleLoadoutStore
{
    /// <summary>
    /// Fills the player's loadouts from their character (loaded with its VehicleLoadouts); a character with none
    /// gets the starter kart and derby car, saved.
    /// </summary>
    public static void Load(Player player, DbCharacter character, IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        if (character.VehicleLoadouts.Count == 0)
        {
            using var dbContext = dbContextFactory.CreateDbContext();

            foreach (var (id, type, slots) in VehicleLoadouts.Defaults())
            {
                var loadout = new DbVehicleLoadout { CharacterId = character.Id, Id = id, Type = type, Slots = [.. slots] };

                dbContext.VehicleLoadouts.Add(loadout);
                character.VehicleLoadouts.Add(loadout);
            }

            character.ActiveKartLoadoutId = 1;
            character.ActiveDerbyLoadoutId = 2;

            dbContext.Characters.Where(x => x.Id == character.Id)
                .ExecuteUpdate(x => x.SetProperty(c => c.ActiveKartLoadoutId, 1).SetProperty(c => c.ActiveDerbyLoadoutId, 2));

            dbContext.SaveChanges();
        }

        player.VehicleStruct = new VehicleStruct
        {
            NextLoadoutId = (uint)(character.VehicleLoadouts.Max(loadout => loadout.Id) + 1),
            Loadouts = character.VehicleLoadouts.OrderBy(loadout => loadout.Id)
                .Select(loadout => VehicleLoadouts.ToPacket(loadout.Id, loadout.Type, loadout.Slots))
                .ToList()
        };

        player.ActiveVehicleLoadout_KartRace = character.ActiveKartLoadoutId;
        player.ActiveVehicleLoadout_DemoDerby = character.ActiveDerbyLoadoutId;
    }

    /// <summary>
    /// The player's active loadout of a type (1 kart racing, 2 demolition derby), or their first of that type.
    /// </summary>
    public static VehicleLoadout? Active(Player player, int type)
    {
        var activeId = type == VehicleLoadouts.DemolitionDerby ? player.ActiveVehicleLoadout_DemoDerby : player.ActiveVehicleLoadout_KartRace;

        return player.VehicleStruct.Loadouts.FirstOrDefault(loadout => loadout.Id == activeId && loadout.Unknown == type)
            ?? player.VehicleStruct.Loadouts.FirstOrDefault(loadout => loadout.Unknown == type);
    }

    public static void SetActive(Player player, int type, int loadoutId, IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        if (type == VehicleLoadouts.DemolitionDerby)
            player.ActiveVehicleLoadout_DemoDerby = loadoutId;
        else
            player.ActiveVehicleLoadout_KartRace = loadoutId;

        using var dbContext = dbContextFactory.CreateDbContext();

        var characterId = GuidHelper.GetPlayerId(player.Guid);

        if (type == VehicleLoadouts.DemolitionDerby)
            dbContext.Characters.Where(x => x.Id == characterId).ExecuteUpdate(x => x.SetProperty(c => c.ActiveDerbyLoadoutId, loadoutId));
        else
            dbContext.Characters.Where(x => x.Id == characterId).ExecuteUpdate(x => x.SetProperty(c => c.ActiveKartLoadoutId, loadoutId));
    }

    /// <summary>
    /// Saves a loadout's slots as they are in the player's list.
    /// </summary>
    public static void Save(Player player, VehicleLoadout loadout, IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var characterId = GuidHelper.GetPlayerId(player.Guid);
        var row = dbContext.VehicleLoadouts.SingleOrDefault(x => x.CharacterId == characterId && x.Id == (int)loadout.Id);

        if (row is null)
        {
            row = new DbVehicleLoadout { CharacterId = characterId, Id = (int)loadout.Id, Type = (int)loadout.Unknown };
            dbContext.VehicleLoadouts.Add(row);
        }

        row.Slots = loadout.Slots.Select(slot => (int)slot).ToList();

        try
        {
            dbContext.SaveChanges();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException($"Couldn't save loadout {loadout.Id} for {player.Name.FullName}.", ex);
        }
    }
}
