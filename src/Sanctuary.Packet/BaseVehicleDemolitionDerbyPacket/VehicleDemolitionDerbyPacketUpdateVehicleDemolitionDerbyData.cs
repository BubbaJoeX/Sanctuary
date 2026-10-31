using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A car's physics state. The client streams its own car with it every frame from game state 6, unreliably; the server
/// sends it for the AI cars (and, between players, relays theirs). A receiver ignores a state older than the last one
/// it applied, by Clock.
/// </summary>
public class VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyData : BaseVehicleDemolitionDerbyPacket, ISerializablePacket, IDeserializable<VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyData>
{
    public new const ushort OpCode = 5;

    /// <summary>
    /// The car: the character guid for a player, the driver NPC's guid for an AI car.
    /// </summary>
    public ulong Guid;

    /// <summary>
    /// Rotation rows, then the position (w 1).
    /// </summary>
    public Matrix4x4 Transform = Matrix4x4.Identity;

    public Vector4 LinearVelocity;

    public Vector4 AngularVelocity;

    /// <summary>
    /// 0 to 1.
    /// </summary>
    public float Throttle;

    /// <summary>
    /// 0 to 1 (also reverse).
    /// </summary>
    public float Brake;

    /// <summary>
    /// -1 to 1.
    /// </summary>
    public float Steering;

    /// <summary>
    /// Four flags the client reads as bools (any byte but 0 is set); kept as bytes, since the live spawn states
    /// carried leftovers in them. Live AI cars sent 0, 0, 0, 1.
    /// </summary>
    public byte Unknown;

    public byte Unknown2;

    public byte Unknown3;

    public byte Unknown4;

    /// <summary>
    /// The sender's clock in milliseconds.
    /// </summary>
    public ulong Clock;

    public VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyData() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        WriteTransform(writer, Transform);
        writer.Write(LinearVelocity);
        writer.Write(AngularVelocity);
        writer.Write(Throttle);
        writer.Write(Brake);
        writer.Write(Steering);
        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
        writer.Write(Clock);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyData value)
    {
        value = new VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyData();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!TryReadTransform(ref reader, out value.Transform))
            return false;

        if (!reader.TryRead(out value.LinearVelocity))
            return false;

        if (!reader.TryRead(out value.AngularVelocity))
            return false;

        if (!reader.TryRead(out value.Throttle))
            return false;

        if (!reader.TryRead(out value.Brake))
            return false;

        if (!reader.TryRead(out value.Steering))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        if (!reader.TryRead(out value.Unknown2))
            return false;

        if (!reader.TryRead(out value.Unknown3))
            return false;

        if (!reader.TryRead(out value.Unknown4))
            return false;

        if (!reader.TryRead(out value.Clock))
            return false;

        return reader.RemainingLength == 0;
    }
}
