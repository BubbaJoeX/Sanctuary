using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The garage opened (on the garage zone's load) or a vehicle was picked there: the client wants the preview vehicle.
/// </summary>
public class VehiclePartPacketRequestVehicleConfig : BaseVehiclePartPacket, IDeserializable<VehiclePartPacketRequestVehicleConfig>
{
    public new const ushort OpCode = 1;

    public VehiclePartPacketRequestVehicleConfig() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehiclePartPacketRequestVehicleConfig value)
    {
        value = new VehiclePartPacketRequestVehicleConfig();

        var reader = new PacketReader(data);

        return value.TryRead(ref reader) && reader.RemainingLength == 0;
    }
}
