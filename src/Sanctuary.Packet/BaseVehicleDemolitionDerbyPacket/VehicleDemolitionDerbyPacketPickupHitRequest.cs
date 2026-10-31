using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The client's car touches a power-up box; sent every frame until answered. It carries no box id: the server finds the
/// box by position.
/// </summary>
public class VehicleDemolitionDerbyPacketPickupHitRequest : BaseVehicleDemolitionDerbyPacket, ISerializablePacket, IDeserializable<VehicleDemolitionDerbyPacketPickupHitRequest>
{
    public new const ushort OpCode = 14;

    /// <summary>
    /// The car: the character guid for a player, the driver NPC's guid for an AI car.
    /// </summary>
    public ulong Guid;

    public Vector4 Position;

    public VehicleDemolitionDerbyPacketPickupHitRequest() : base(OpCode)
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

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleDemolitionDerbyPacketPickupHitRequest value)
    {
        value = new VehicleDemolitionDerbyPacketPickupHitRequest();

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
