using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Replaces all of the player's loadouts.
/// </summary>
public class VehicleRefreshConfigsPacket : BaseVehicleLoadoutPacket, ISerializablePacket
{
    public new const byte OpCode = 3;

    public VehicleStruct Vehicles = new();

    public VehicleRefreshConfigsPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Vehicles.Serialize(writer);

        return writer.Buffer;
    }
}
