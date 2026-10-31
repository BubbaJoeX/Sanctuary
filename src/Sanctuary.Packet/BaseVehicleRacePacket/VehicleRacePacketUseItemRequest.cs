using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The player uses the item at the front of their kart's queue. It carries no item: the server keeps the queue.
/// </summary>
public class VehicleRacePacketUseItemRequest : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketUseItemRequest>
{
    public new const ushort OpCode = 19;

    public ulong Guid;

    public VehicleRacePacketUseItemRequest() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketUseItemRequest value)
    {
        value = new VehicleRacePacketUseItemRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        return reader.RemainingLength == 0;
    }
}
