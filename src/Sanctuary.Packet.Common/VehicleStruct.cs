using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// All of a player's vehicle loadouts, as ClientPcData and VehicleRefreshConfigs carry them.
/// </summary>
public class VehicleStruct : ISerializableType
{
    public uint NextLoadoutId;

    public List<VehicleLoadout> Loadouts = [];

    public void Serialize(PacketWriter writer)
    {
        writer.Write(NextLoadoutId);

        writer.Write(Loadouts.Count);

        foreach (var loadout in Loadouts)
        {
            writer.Write(loadout.Id);
            loadout.Serialize(writer);
        }
    }
}
