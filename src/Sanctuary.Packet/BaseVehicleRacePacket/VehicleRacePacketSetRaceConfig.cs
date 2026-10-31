using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The track's minimap picture and the finish camera.
/// </summary>
public class VehicleRacePacketSetRaceConfig : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketSetRaceConfig>
{
    public new const ushort OpCode = 34;

    /// <summary>
    /// Live the lap count; the HUD uses the progress data's.
    /// </summary>
    public uint Laps;

    /// <summary>
    /// The minimap picture.
    /// </summary>
    public uint TrackId;

    /// <summary>
    /// Seconds after the player's finish before the camera moves.
    /// </summary>
    public float FinishCameraDelay;

    public Matrix4x4 FinishCameraTransform = Matrix4x4.Identity;

    public uint MissileExplosionEffectId;

    public uint MineExplosionEffectId;

    public VehicleRacePacketSetRaceConfig() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Laps);
        writer.Write(TrackId);
        writer.Write(FinishCameraDelay);
        WriteTransform(writer, FinishCameraTransform);
        writer.Write(MissileExplosionEffectId);
        writer.Write(MineExplosionEffectId);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketSetRaceConfig value)
    {
        value = new VehicleRacePacketSetRaceConfig();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Laps))
            return false;

        if (!reader.TryRead(out value.TrackId))
            return false;

        if (!reader.TryRead(out value.FinishCameraDelay))
            return false;

        if (!TryReadTransform(ref reader, out value.FinishCameraTransform))
            return false;

        if (!reader.TryRead(out value.MissileExplosionEffectId))
            return false;

        if (!reader.TryRead(out value.MineExplosionEffectId))
            return false;

        return reader.RemainingLength == 0;
    }
}
