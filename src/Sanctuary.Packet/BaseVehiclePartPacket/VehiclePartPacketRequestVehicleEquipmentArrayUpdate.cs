using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The player equips a part from their inventory on the garage's vehicle.
/// </summary>
public class VehiclePartPacketRequestVehicleEquipmentArrayUpdate : BaseVehiclePartPacket, IDeserializable<VehiclePartPacketRequestVehicleEquipmentArrayUpdate>
{
    public new const ushort OpCode = 3;

    public uint ItemId;

    public VehiclePartPacketRequestVehicleEquipmentArrayUpdate() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehiclePartPacketRequestVehicleEquipmentArrayUpdate value)
    {
        value = new VehiclePartPacketRequestVehicleEquipmentArrayUpdate();

        var reader = new PacketReader(data);

        return value.TryRead(ref reader) && reader.TryRead(out value.ItemId) && reader.RemainingLength == 0;
    }
}
