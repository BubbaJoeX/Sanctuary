using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// An item box on the track. Ignored if the id is already there.
/// </summary>
public class VehicleRacePacketCreateProxiedVehicleRacePickup : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketCreateProxiedVehicleRacePickup>
{
    public new const ushort OpCode = 15;

    public uint PickupId;

    /// <summary>
    /// A Models.txt id.
    /// </summary>
    public uint ModelId;

    public Vector4 Position;

    /// <summary>
    /// Its trigger sphere.
    /// </summary>
    public float Radius;

    /// <summary>
    /// Live 1324.
    /// </summary>
    public uint Unknown;

    public VehicleRacePacketCreateProxiedVehicleRacePickup() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PickupId);
        writer.Write(ModelId);
        writer.Write(Position);
        writer.Write(Radius);
        writer.Write(Unknown);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketCreateProxiedVehicleRacePickup value)
    {
        value = new VehicleRacePacketCreateProxiedVehicleRacePickup();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.PickupId))
            return false;

        if (!reader.TryRead(out value.ModelId))
            return false;

        if (!reader.TryRead(out value.Position))
            return false;

        if (!reader.TryRead(out value.Radius))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        return reader.RemainingLength == 0;
    }
}
