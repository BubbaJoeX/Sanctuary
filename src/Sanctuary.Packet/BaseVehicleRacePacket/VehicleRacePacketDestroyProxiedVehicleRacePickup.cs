using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Removes an item box (taken).
/// </summary>
public class VehicleRacePacketDestroyProxiedVehicleRacePickup : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketDestroyProxiedVehicleRacePickup>
{
    public new const ushort OpCode = 16;

    public uint PickupId;

    public VehicleRacePacketDestroyProxiedVehicleRacePickup() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PickupId);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketDestroyProxiedVehicleRacePickup value)
    {
        value = new VehicleRacePacketDestroyProxiedVehicleRacePickup();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.PickupId))
            return false;

        return reader.RemainingLength == 0;
    }
}
