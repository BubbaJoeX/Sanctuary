using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A crash between two cars: the client reports its own car's contacts; sent to a client it plays the impact on both.
/// </summary>
public class VehicleDemolitionDerbyPacketVehicleHit : BaseVehicleDemolitionDerbyPacket, ISerializablePacket, IDeserializable<VehicleDemolitionDerbyPacketVehicleHit>
{
    public new const ushort OpCode = 16;

    public ulong Guid;

    public ulong OtherGuid;

    public float Impulse;

    public VehicleDemolitionDerbyPacketVehicleHit() : base(OpCode)
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

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleDemolitionDerbyPacketVehicleHit value)
    {
        value = new VehicleDemolitionDerbyPacketVehicleHit();

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
