using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Adds or replaces one of the player's loadouts.
/// </summary>
public class VehicleUpdateConfigPacket : BaseVehicleLoadoutPacket, ISerializablePacket
{
    public new const byte OpCode = 1;

    public VehicleLoadout Loadout = new();

    public VehicleUpdateConfigPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Loadout.Serialize(writer);

        return writer.Buffer;
    }
}
