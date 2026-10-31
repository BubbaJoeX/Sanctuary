using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A soccer player's animation state (1 idle, 2 run, 11 run with the ball, 17-34 kicks, 35-38 tackles, 39-44 knocked
/// down, 45-50 celebrating or not, 51-63 the goalie's). The client sends its own as it changes.
/// </summary>
public class SoccerPacketUpdatePlayerState : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketUpdatePlayerState>
{
    public new const ushort OpCode = 14;

    public ulong Guid;

    public byte State;

    /// <summary>
    /// 0 to 100.
    /// </summary>
    public byte Charge;

    /// <summary>
    /// Counts up per player.
    /// </summary>
    public uint Sequence;

    public Vector4 Unknown;

    public Vector4 Facing;

    public Vector4 Position;

    public SoccerPacketUpdatePlayerState() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(State);
        writer.Write(Charge);
        writer.Write(Sequence);
        writer.Write(Unknown);
        writer.Write(Facing);
        writer.Write(Position);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketUpdatePlayerState value)
    {
        value = new SoccerPacketUpdatePlayerState();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.State))
            return false;

        if (!reader.TryRead(out value.Charge))
            return false;

        if (!reader.TryRead(out value.Sequence))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        if (!reader.TryRead(out value.Facing))
            return false;

        if (!reader.TryRead(out value.Position))
            return false;

        return reader.RemainingLength == 0;
    }
}
