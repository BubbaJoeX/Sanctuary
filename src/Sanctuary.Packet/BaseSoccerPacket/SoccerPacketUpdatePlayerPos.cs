using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A soccer player's position. The client sends its own about 7 times a second; the server places everyone with Snap at
/// a kick-off.
/// </summary>
public class SoccerPacketUpdatePlayerPos : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketUpdatePlayerPos>
{
    public new const ushort OpCode = 1;

    public ulong Guid;

    public Vector4 Position;

    /// <summary>
    /// Which way they're moving.
    /// </summary>
    public Vector4 Direction;

    public Vector4 Facing;

    /// <summary>
    /// The match clock (UpdateGameTime's).
    /// </summary>
    public float Clock;

    public float Speed;

    /// <summary>
    /// Teleports them there.
    /// </summary>
    public bool Snap;

    public SoccerPacketUpdatePlayerPos() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Position);
        writer.Write(Direction);
        writer.Write(Facing);
        writer.Write(Clock);
        writer.Write(Speed);
        writer.Write(Snap);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketUpdatePlayerPos value)
    {
        value = new SoccerPacketUpdatePlayerPos();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Position))
            return false;

        if (!reader.TryRead(out value.Direction))
            return false;

        if (!reader.TryRead(out value.Facing))
            return false;

        if (!reader.TryRead(out value.Clock))
            return false;

        if (!reader.TryRead(out value.Speed))
            return false;

        if (!reader.TryRead(out value.Snap))
            return false;

        return reader.RemainingLength == 0;
    }
}
