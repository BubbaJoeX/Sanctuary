using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The pitch, the ball and the soccer controller's tuning. The client keeps the whole body and creates the ball from
/// it. No client string names the tuning values; they are the live game's.
/// </summary>
public class SoccerPacketSetClientConfig : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketSetClientConfig>
{
    public new const ushort OpCode = 7;

    /// <summary>
    /// Min x, min z, max x, max z.
    /// </summary>
    public Vector4 PitchBounds;

    /// <summary>
    /// Sixteen floats.
    /// </summary>
    public float[] Tuning = new float[16];

    /// <summary>
    /// Eleven floats.
    /// </summary>
    public float[] Tuning2 = new float[11];

    public string BallModel = string.Empty;

    public float Unknown;
    public float Unknown2;

    /// <summary>
    /// The centre spot, where the ball is put for a kick-off.
    /// </summary>
    public Vector4 BallSpawn;

    /// <summary>
    /// Nine floats.
    /// </summary>
    public float[] Tuning3 = new float[9];

    public uint Unknown3;
    public uint Unknown4;
    public float Unknown5;

    public SoccerPacketSetClientConfig() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PitchBounds);

        foreach (var value in Tuning)
            writer.Write(value);

        foreach (var value in Tuning2)
            writer.Write(value);

        writer.Write(BallModel);
        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(BallSpawn);

        foreach (var value in Tuning3)
            writer.Write(value);

        writer.Write(Unknown3);
        writer.Write(Unknown4);
        writer.Write(Unknown5);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketSetClientConfig value)
    {
        value = new SoccerPacketSetClientConfig();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader) || !reader.TryRead(out value.PitchBounds))
            return false;

        if (!TryReadFloats(ref reader, value.Tuning) || !TryReadFloats(ref reader, value.Tuning2))
            return false;

        if (!reader.TryRead(out value.BallModel) || !reader.TryRead(out value.Unknown) || !reader.TryRead(out value.Unknown2))
            return false;

        if (!reader.TryRead(out value.BallSpawn) || !TryReadFloats(ref reader, value.Tuning3))
            return false;

        if (!reader.TryRead(out value.Unknown3) || !reader.TryRead(out value.Unknown4) || !reader.TryRead(out value.Unknown5))
            return false;

        return reader.RemainingLength == 0;
    }

    private static bool TryReadFloats(ref PacketReader reader, float[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (!reader.TryRead(out values[i]))
                return false;
        }

        return true;
    }
}
