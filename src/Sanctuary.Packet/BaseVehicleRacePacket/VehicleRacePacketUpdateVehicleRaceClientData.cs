using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Another kart's physics state, addressed by its grid slot (bits 3-5 of Flags). Ignored for the client's own kart.
/// </summary>
public class VehicleRacePacketUpdateVehicleRaceClientData : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketUpdateVehicleRaceClientData>
{
    public new const ushort OpCode = 5;

    /// <summary>
    /// Rotation rows and position, three floats each.
    /// </summary>
    public Matrix4x4 Transform = Matrix4x4.Identity;

    public Vector3 LinearVelocity;

    public Vector3 AngularVelocity;

    public float Steering;

    /// <summary>
    /// 0x40 throttle, 0x80 brake, bits 3-5 the grid slot.
    /// </summary>
    public byte Flags;

    /// <summary>
    /// The sender's clock in milliseconds.
    /// </summary>
    public ulong Clock;

    public VehicleRacePacketUpdateVehicleRaceClientData() : base(OpCode)
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

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketUpdateVehicleRaceClientData value)
    {
        value = new VehicleRacePacketUpdateVehicleRaceClientData();

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
