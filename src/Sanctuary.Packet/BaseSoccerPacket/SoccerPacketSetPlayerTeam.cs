using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Puts a soccer player on a team, in a position (3 the goalie).
/// </summary>
public class SoccerPacketSetPlayerTeam : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketSetPlayerTeam>
{
    public new const ushort OpCode = 6;

    public ulong Guid;

    public uint Team;

    public uint Position;

    public SoccerPacketSetPlayerTeam() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Team);
        writer.Write(Position);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketSetPlayerTeam value)
    {
        value = new SoccerPacketSetPlayerTeam();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Team))
            return false;

        if (!reader.TryRead(out value.Position))
            return false;

        return reader.RemainingLength == 0;
    }
}
