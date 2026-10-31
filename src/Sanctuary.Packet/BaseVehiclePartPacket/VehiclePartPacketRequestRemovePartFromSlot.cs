using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The player takes the part in a slot (0-15) off the garage's vehicle.
/// </summary>
public class VehiclePartPacketRequestRemovePartFromSlot : BaseVehiclePartPacket, IDeserializable<VehiclePartPacketRequestRemovePartFromSlot>
{
    public new const ushort OpCode = 6;

    public uint Slot;

    public VehiclePartPacketRequestRemovePartFromSlot() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehiclePartPacketRequestRemovePartFromSlot value)
    {
        value = new VehiclePartPacketRequestRemovePartFromSlot();

        var reader = new PacketReader(data);

        return value.TryRead(ref reader) && reader.TryRead(out value.Slot) && reader.RemainingLength == 0;
    }
}
