using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The client's kart touches an item box; sent every frame until answered. It carries no box id: the server finds the
/// box by position.
/// </summary>
public class VehicleRacePacketPickupHitRequest : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketPickupHitRequest>
{
    public new const ushort OpCode = 17;

    public ulong Guid;

    public Vector4 Position;

    public VehicleRacePacketPickupHitRequest() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Position);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketPickupHitRequest value)
    {
        value = new VehicleRacePacketPickupHitRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Position))
            return false;

        return reader.RemainingLength == 0;
    }
}
