using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Removes a car and shrinks the race order (a player who left).
/// </summary>
public class VehicleDemolitionDerbyPacketKickProxiedVehicleDemolitionDerby : BaseVehicleDemolitionDerbyPacket, ISerializablePacket, IDeserializable<VehicleDemolitionDerbyPacketKickProxiedVehicleDemolitionDerby>
{
    public new const ushort OpCode = 9;

    /// <summary>
    /// The car: the character guid for a player, the driver NPC's guid for an AI car.
    /// </summary>
    public ulong Guid;

    public VehicleDemolitionDerbyPacketKickProxiedVehicleDemolitionDerby() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleDemolitionDerbyPacketKickProxiedVehicleDemolitionDerby value)
    {
        value = new VehicleDemolitionDerbyPacketKickProxiedVehicleDemolitionDerby();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        return reader.RemainingLength == 0;
    }
}
