using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Removes a kart and shrinks the race order (a player who left).
/// </summary>
public class VehicleRacePacketKickProxiedVehicleRace : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketKickProxiedVehicleRace>
{
    public new const ushort OpCode = 13;

    public ulong Guid;

    public VehicleRacePacketKickProxiedVehicleRace() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketKickProxiedVehicleRace value)
    {
        value = new VehicleRacePacketKickProxiedVehicleRace();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        return reader.RemainingLength == 0;
    }
}
