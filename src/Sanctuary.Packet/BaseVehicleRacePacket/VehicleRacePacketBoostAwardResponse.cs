using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Gives the player's kart a boost item (5).
/// </summary>
public class VehicleRacePacketBoostAwardResponse : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketBoostAwardResponse>
{
    public new const ushort OpCode = 23;

    public VehicleRacePacketBoostAwardResponse() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketBoostAwardResponse value)
    {
        value = new VehicleRacePacketBoostAwardResponse();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}
