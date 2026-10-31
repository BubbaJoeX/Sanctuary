using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// How many cars are players' (game state 3 waits until the client has that many).
/// </summary>
public class VehicleDemolitionDerbyPacketUpdatePlayerCount : BaseVehicleDemolitionDerbyPacket, ISerializablePacket, IDeserializable<VehicleDemolitionDerbyPacketUpdatePlayerCount>
{
    public new const ushort OpCode = 3;

    public uint Count;

    public VehicleDemolitionDerbyPacketUpdatePlayerCount() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Count);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleDemolitionDerbyPacketUpdatePlayerCount value)
    {
        value = new VehicleDemolitionDerbyPacketUpdatePlayerCount();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Count))
            return false;

        return reader.RemainingLength == 0;
    }
}
