using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Removes a power-up box (taken).
/// </summary>
public class VehicleDemolitionDerbyPacketDestroyProxiedVehicleDemolitionDerbyPickup : BaseVehicleDemolitionDerbyPacket, ISerializablePacket, IDeserializable<VehicleDemolitionDerbyPacketDestroyProxiedVehicleDemolitionDerbyPickup>
{
    public new const ushort OpCode = 13;

    public uint PickupId;

    public VehicleDemolitionDerbyPacketDestroyProxiedVehicleDemolitionDerbyPickup() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PickupId);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleDemolitionDerbyPacketDestroyProxiedVehicleDemolitionDerbyPickup value)
    {
        value = new VehicleDemolitionDerbyPacketDestroyProxiedVehicleDemolitionDerbyPickup();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.PickupId))
            return false;

        return reader.RemainingLength == 0;
    }
}
