using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Removes a kart.
/// </summary>
public class VehicleRacePacketDestroyProxiedVehicleRace : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketDestroyProxiedVehicleRace>
{
    public new const ushort OpCode = 12;

    public ulong Guid;

    public VehicleRacePacketDestroyProxiedVehicleRace() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketDestroyProxiedVehicleRace value)
    {
        value = new VehicleRacePacketDestroyProxiedVehicleRace();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        return reader.RemainingLength == 0;
    }
}
