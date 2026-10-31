using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The player's active vehicle profile for a type (1 kart racing, 2 demolition derby), which the client's
/// GetActiveVehicleProfile reads back. No shipped client script sends it.
/// </summary>
public class CommandPacketSetActiveVehicleGuid : BaseCommandPacket, ISerializablePacket, IDeserializable<CommandPacketSetActiveVehicleGuid>
{
    public new const short OpCode = 31;

    public uint VehicleType;

    public uint VehicleId;

    public CommandPacketSetActiveVehicleGuid() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(VehicleType);
        writer.Write(VehicleId);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out CommandPacketSetActiveVehicleGuid value)
    {
        value = new CommandPacketSetActiveVehicleGuid();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.VehicleType) || !reader.TryRead(out value.VehicleId))
            return false;

        return reader.RemainingLength == 0;
    }
}
