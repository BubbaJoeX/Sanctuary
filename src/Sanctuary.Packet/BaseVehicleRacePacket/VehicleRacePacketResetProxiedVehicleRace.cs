using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Puts a kart back on the track (fallen off, stuck).
/// </summary>
public class VehicleRacePacketResetProxiedVehicleRace : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketResetProxiedVehicleRace>
{
    public new const ushort OpCode = 33;

    public ulong Guid;

    /// <summary>
    /// Rotation rows and position, three floats each.
    /// </summary>
    public Matrix4x4 Transform = Matrix4x4.Identity;

    public VehicleRacePacketResetProxiedVehicleRace() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        WriteCompactTransform(writer, Transform);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketResetProxiedVehicleRace value)
    {
        value = new VehicleRacePacketResetProxiedVehicleRace();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!TryReadCompactTransform(ref reader, out value.Transform))
            return false;

        return reader.RemainingLength == 0;
    }
}
