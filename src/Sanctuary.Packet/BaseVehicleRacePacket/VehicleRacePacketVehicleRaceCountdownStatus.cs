using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A countdown number. The client ignores the server's guid, shows the count and echoes it back with its own guid.
/// </summary>
public class VehicleRacePacketVehicleRaceCountdownStatus : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketVehicleRaceCountdownStatus>
{
    public new const ushort OpCode = 10;

    public ulong Guid;

    public uint Count;

    public VehicleRacePacketVehicleRaceCountdownStatus() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Count);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketVehicleRaceCountdownStatus value)
    {
        value = new VehicleRacePacketVehicleRaceCountdownStatus();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Count))
            return false;

        return reader.RemainingLength == 0;
    }
}
