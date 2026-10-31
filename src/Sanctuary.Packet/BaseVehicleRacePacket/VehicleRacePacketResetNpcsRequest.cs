using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A debug request with no caller in the client.
/// </summary>
public class VehicleRacePacketResetNpcsRequest : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketResetNpcsRequest>
{
    public new const ushort OpCode = 32;

    public VehicleRacePacketResetNpcsRequest() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketResetNpcsRequest value)
    {
        value = new VehicleRacePacketResetNpcsRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}
