using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// What the player's car got from a box: 1 health (the health itself comes in the state data), 2 boost (A), 3 shield
/// (C), 4 attack up (B), 5 ultimate (B).
/// </summary>
public class VehicleDemolitionDerbyPacketPickupHitResponse : BaseVehicleDemolitionDerbyPacket, ISerializablePacket, IDeserializable<VehicleDemolitionDerbyPacketPickupHitResponse>
{
    public new const ushort OpCode = 15;

    /// <summary>
    /// The car: the character guid for a player, the driver NPC's guid for an AI car.
    /// </summary>
    public ulong Guid;

    public uint Item;

    public float A;

    public float B;

    public float C;

    public VehicleDemolitionDerbyPacketPickupHitResponse() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Item);
        writer.Write(A);
        writer.Write(B);
        writer.Write(C);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleDemolitionDerbyPacketPickupHitResponse value)
    {
        value = new VehicleDemolitionDerbyPacketPickupHitResponse();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Item))
            return false;

        if (!reader.TryRead(out value.A))
            return false;

        if (!reader.TryRead(out value.B))
            return false;

        if (!reader.TryRead(out value.C))
            return false;

        return reader.RemainingLength == 0;
    }
}
