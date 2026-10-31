using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A debug label over an AI player.
/// </summary>
public class SoccerPacketSendAIInfo : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketSendAIInfo>
{
    public new const ushort OpCode = 24;

    public ulong Guid;

    public string Info = string.Empty;

    public SoccerPacketSendAIInfo() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Info);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketSendAIInfo value)
    {
        value = new SoccerPacketSendAIInfo();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Info))
            return false;

        return reader.RemainingLength == 0;
    }
}
