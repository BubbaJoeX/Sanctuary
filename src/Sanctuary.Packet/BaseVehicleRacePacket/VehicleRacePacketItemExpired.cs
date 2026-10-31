using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A timed item's effect ends (2 or 4).
/// </summary>
public class VehicleRacePacketItemExpired : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketItemExpired>
{
    public new const ushort OpCode = 21;

    public uint Item;

    public VehicleRacePacketItemExpired() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Item);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketItemExpired value)
    {
        value = new VehicleRacePacketItemExpired();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Item))
            return false;

        return reader.RemainingLength == 0;
    }
}
