using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A timed power-up on the player's car ends: 3 shield, 4 attack up, 5 ultimate.
/// </summary>
public class VehicleDemolitionDerbyPacketItemExpired : BaseVehicleDemolitionDerbyPacket, ISerializablePacket, IDeserializable<VehicleDemolitionDerbyPacketItemExpired>
{
    public new const ushort OpCode = 17;

    public uint Item;

    public VehicleDemolitionDerbyPacketItemExpired() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Item);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleDemolitionDerbyPacketItemExpired value)
    {
        value = new VehicleDemolitionDerbyPacketItemExpired();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Item))
            return false;

        return reader.RemainingLength == 0;
    }
}
