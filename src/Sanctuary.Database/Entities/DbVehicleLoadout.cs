using System.Collections.Generic;

namespace Sanctuary.Database.Entities;

/// <summary>
/// A kart or derby car as a character has kitted it out in the garage: the item in each of its 16 slots.
/// </summary>
public class DbVehicleLoadout
{
    /// <summary>
    /// The character's own number for it (the client's loadout id).
    /// </summary>
    public int Id { get; set; }

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;

    /// <summary>
    /// 1 kart racing, 2 demolition derby.
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// The item definition in each slot (0 chassis, 1 engine ... 8 decal ... 15 gear), 0 for none.
    /// </summary>
    public List<int> Slots { get; set; } = [];
}
