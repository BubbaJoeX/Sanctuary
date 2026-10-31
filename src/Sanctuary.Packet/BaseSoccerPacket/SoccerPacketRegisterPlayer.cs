using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Adds (or with Remove takes away) a soccer player on an actor already spawned. The client sends one with no guid when
/// it is ready to play.
/// </summary>
public class SoccerPacketRegisterPlayer : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketRegisterPlayer>
{
    public new const ushort OpCode = 3;

    public ulong Guid;

    public bool Remove;

    public bool IsAi;

    public SoccerPacketRegisterPlayer() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Remove);
        writer.Write(IsAi);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketRegisterPlayer value)
    {
        value = new SoccerPacketRegisterPlayer();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Remove))
            return false;

        if (!reader.TryRead(out value.IsAi))
            return false;

        return reader.RemainingLength == 0;
    }
}
