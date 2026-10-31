using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The item being used: the client plays it and takes it out of its slots.
/// </summary>
public class VehicleRacePacketUseItemResponse : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketUseItemResponse>
{
    public new const ushort OpCode = 20;

    public uint Item;

    public VehicleRacePacketUseItemResponse() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Item);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketUseItemResponse value)
    {
        value = new VehicleRacePacketUseItemResponse();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Item))
            return false;

        return reader.RemainingLength == 0;
    }
}
