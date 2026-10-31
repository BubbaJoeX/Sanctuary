using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The player picks the vehicle (a loadout) they race or derby in, in the garage or on a game's start screen.
/// </summary>
public class VehiclePartPacketRequestSelectVehicle : BaseVehiclePartPacket, IDeserializable<VehiclePartPacketRequestSelectVehicle>
{
    public new const ushort OpCode = 5;

    public uint VehicleId;

    /// <summary>
    /// 1 kart racing, 2 demolition derby.
    /// </summary>
    public uint VehicleType;

    public VehiclePartPacketRequestSelectVehicle() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehiclePartPacketRequestSelectVehicle value)
    {
        value = new VehiclePartPacketRequestSelectVehicle();

        var reader = new PacketReader(data);

        return value.TryRead(ref reader) && reader.TryRead(out value.VehicleId) && reader.TryRead(out value.VehicleType)
            && reader.RemainingLength == 0;
    }
}
