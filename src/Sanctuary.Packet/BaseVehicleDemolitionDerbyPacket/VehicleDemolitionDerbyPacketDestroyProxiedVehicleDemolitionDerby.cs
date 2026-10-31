using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Removes a car.
/// </summary>
public class VehicleDemolitionDerbyPacketDestroyProxiedVehicleDemolitionDerby : BaseVehicleDemolitionDerbyPacket, ISerializablePacket, IDeserializable<VehicleDemolitionDerbyPacketDestroyProxiedVehicleDemolitionDerby>
{
    public new const ushort OpCode = 8;

    /// <summary>
    /// The car: the character guid for a player, the driver NPC's guid for an AI car.
    /// </summary>
    public ulong Guid;

    public VehicleDemolitionDerbyPacketDestroyProxiedVehicleDemolitionDerby() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleDemolitionDerbyPacketDestroyProxiedVehicleDemolitionDerby value)
    {
        value = new VehicleDemolitionDerbyPacketDestroyProxiedVehicleDemolitionDerby();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        return reader.RemainingLength == 0;
    }
}
