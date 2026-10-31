using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The ball, simulated by the server: sent while it is loose, not while someone holds it.
/// </summary>
public class SoccerPacketUpdateSoccerBall : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketUpdateSoccerBall>
{
    public new const ushort OpCode = 4;

    /// <summary>
    /// Rotation rows, then the position (w 1).
    /// </summary>
    public Matrix4x4 Transform = Matrix4x4.Identity;

    public Vector4 Velocity;

    /// <summary>
    /// Live 0 on the first update, 1 at a goal, 255 otherwise.
    /// </summary>
    public byte Unknown;

    /// <summary>
    /// Bits 0-2 the ball's state (bit 2 on a lob), bits 3-4 which ball (0-2).
    /// </summary>
    public byte Flags;

    public float Unknown2;

    public float Unknown3;

    /// <summary>
    /// Puts it there at once.
    /// </summary>
    public bool Snap;

    public SoccerPacketUpdateSoccerBall() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        WriteTransform(writer, Transform);
        writer.Write(Velocity);
        writer.Write(Unknown);
        writer.Write(Flags);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Snap);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketUpdateSoccerBall value)
    {
        value = new SoccerPacketUpdateSoccerBall();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!TryReadTransform(ref reader, out value.Transform))
            return false;

        if (!reader.TryRead(out value.Velocity))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        if (!reader.TryRead(out value.Flags))
            return false;

        if (!reader.TryRead(out value.Unknown2))
            return false;

        if (!reader.TryRead(out value.Unknown3))
            return false;

        if (!reader.TryRead(out value.Snap))
            return false;

        return reader.RemainingLength == 0;
    }
}
