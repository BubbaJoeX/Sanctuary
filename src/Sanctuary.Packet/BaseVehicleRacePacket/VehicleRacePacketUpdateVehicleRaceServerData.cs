using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The client's own kart, every frame from game state 6, unreliably (the same layout as UpdateVehicleRaceClientData).
/// </summary>
public class VehicleRacePacketUpdateVehicleRaceServerData : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketUpdateVehicleRaceServerData>
{
    public new const ushort OpCode = 6;

    /// <summary>
    /// Rotation rows and position, three floats each.
    /// </summary>
    public Matrix4x4 Transform = Matrix4x4.Identity;

    public Vector3 LinearVelocity;

    public Vector3 AngularVelocity;

    public float Steering;

    /// <summary>
    /// 0x40 throttle, 0x80 brake.
    /// </summary>
    public byte Flags;

    /// <summary>
    /// The client's clock in milliseconds.
    /// </summary>
    public ulong Clock;

    public VehicleRacePacketUpdateVehicleRaceServerData() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        WriteCompactTransform(writer, Transform);
        writer.Write(LinearVelocity);
        writer.Write(AngularVelocity);
        writer.Write(Steering);
        writer.Write(Flags);
        writer.Write(Clock);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketUpdateVehicleRaceServerData value)
    {
        value = new VehicleRacePacketUpdateVehicleRaceServerData();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!TryReadCompactTransform(ref reader, out value.Transform))
            return false;

        if (!reader.TryRead(out value.LinearVelocity))
            return false;

        if (!reader.TryRead(out value.AngularVelocity))
            return false;

        if (!reader.TryRead(out value.Steering))
            return false;

        if (!reader.TryRead(out value.Flags))
            return false;

        if (!reader.TryRead(out value.Clock))
            return false;

        return reader.RemainingLength == 0;
    }
}
