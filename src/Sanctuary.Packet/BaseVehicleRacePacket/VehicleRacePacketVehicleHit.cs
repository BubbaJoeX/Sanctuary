using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A bump between karts: the client reports its own kart's; sent to a client it pushes the second kart away from the
/// first by Impulse and plays the impact.
/// </summary>
public class VehicleRacePacketVehicleHit : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketVehicleHit>
{
    public new const ushort OpCode = 8;

    public ulong Guid;

    public ulong OtherGuid;

    public float Impulse;

    public VehicleRacePacketVehicleHit() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(OtherGuid);
        writer.Write(Impulse);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketVehicleHit value)
    {
        value = new VehicleRacePacketVehicleHit();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.OtherGuid))
            return false;

        if (!reader.TryRead(out value.Impulse))
            return false;

        return reader.RemainingLength == 0;
    }
}
