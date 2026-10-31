using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// How many karts are AI (game state 4 waits until the client has that many).
/// </summary>
public class VehicleRacePacketUpdateNpcCount : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketUpdateNpcCount>
{
    public new const ushort OpCode = 4;

    public uint Count;

    public VehicleRacePacketUpdateNpcCount() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Count);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketUpdateNpcCount value)
    {
        value = new VehicleRacePacketUpdateNpcCount();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Count))
            return false;

        return reader.RemainingLength == 0;
    }
}
