using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A countdown number. The client ignores the server's guid, shows the count and echoes it back with its own guid.
/// </summary>
public class VehicleDemolitionDerbyPacketVehicleDemolitionDerbyCountdownStatus : BaseVehicleDemolitionDerbyPacket, ISerializablePacket, IDeserializable<VehicleDemolitionDerbyPacketVehicleDemolitionDerbyCountdownStatus>
{
    public new const ushort OpCode = 7;

    public ulong Guid;

    public int Count;

    public VehicleDemolitionDerbyPacketVehicleDemolitionDerbyCountdownStatus() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Count);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleDemolitionDerbyPacketVehicleDemolitionDerbyCountdownStatus value)
    {
        value = new VehicleDemolitionDerbyPacketVehicleDemolitionDerbyCountdownStatus();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Count))
            return false;

        return reader.RemainingLength == 0;
    }
}
