using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A power-up box on the arena floor. Ignored if the id is already there.
/// </summary>
public class VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerbyPickup : BaseVehicleDemolitionDerbyPacket, ISerializablePacket, IDeserializable<VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerbyPickup>
{
    public new const ushort OpCode = 12;

    public uint PickupId;

    /// <summary>
    /// A Models.txt id.
    /// </summary>
    public uint ModelId;

    public Vector4 Position;

    public float Unknown;

    public uint Unknown2;

    public VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerbyPickup() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PickupId);
        writer.Write(ModelId);
        writer.Write(Position);
        writer.Write(Unknown);
        writer.Write(Unknown2);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerbyPickup value)
    {
        value = new VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerbyPickup();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.PickupId))
            return false;

        if (!reader.TryRead(out value.ModelId))
            return false;

        if (!reader.TryRead(out value.Position))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        if (!reader.TryRead(out value.Unknown2))
            return false;

        return reader.RemainingLength == 0;
    }
}
