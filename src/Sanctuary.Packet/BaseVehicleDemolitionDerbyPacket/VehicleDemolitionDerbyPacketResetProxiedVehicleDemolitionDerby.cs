using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Puts a wrecked car back at its spawn, repaired.
/// </summary>
public class VehicleDemolitionDerbyPacketResetProxiedVehicleDemolitionDerby : BaseVehicleDemolitionDerbyPacket, ISerializablePacket, IDeserializable<VehicleDemolitionDerbyPacketResetProxiedVehicleDemolitionDerby>
{
    public new const ushort OpCode = 18;

    /// <summary>
    /// The car: the character guid for a player, the driver NPC's guid for an AI car.
    /// </summary>
    public ulong Guid;

    public VehicleDemolitionDerbyPacketResetProxiedVehicleDemolitionDerby() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleDemolitionDerbyPacketResetProxiedVehicleDemolitionDerby value)
    {
        value = new VehicleDemolitionDerbyPacketResetProxiedVehicleDemolitionDerby();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        return reader.RemainingLength == 0;
    }
}
