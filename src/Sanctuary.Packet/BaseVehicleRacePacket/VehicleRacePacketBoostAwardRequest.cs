using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The player's boost meter filled: they ask for a boost item.
/// </summary>
public class VehicleRacePacketBoostAwardRequest : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketBoostAwardRequest>
{
    public new const ushort OpCode = 22;

    public ulong Guid;

    public VehicleRacePacketBoostAwardRequest() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketBoostAwardRequest value)
    {
        value = new VehicleRacePacketBoostAwardRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        return reader.RemainingLength == 0;
    }
}
