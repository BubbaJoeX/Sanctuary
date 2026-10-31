using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A soccer player's stick and buttons. The client sends its own as they change; the server drives the AI players with
/// it, which the client moves as it would a player.
/// </summary>
public class SoccerPacketUpdatePlayerControls : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketUpdatePlayerControls>
{
    public new const ushort OpCode = 16;

    public ulong Guid;

    public float X;

    public float Y;

    public uint Buttons;

    public SoccerPacketUpdatePlayerControls() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(X);
        writer.Write(Y);
        writer.Write(Buttons);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketUpdatePlayerControls value)
    {
        value = new SoccerPacketUpdatePlayerControls();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.X))
            return false;

        if (!reader.TryRead(out value.Y))
            return false;

        if (!reader.TryRead(out value.Buttons))
            return false;

        return reader.RemainingLength == 0;
    }
}
