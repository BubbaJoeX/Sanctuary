using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A ping on the minimap: the client sends it (grouped or not) when its player clicks the map, and shows the ones it
/// gets at their world X and Z. The guid the server sends isn't read.
/// </summary>
public class GroupPacketMapPing : BaseGroupPacket, ISerializablePacket, IDeserializable<GroupPacketMapPing>
{
    public new const short OpCode = 16;

    public ulong Guid;

    public int X;
    public int Z;

    public GroupPacketMapPing() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(X);
        writer.Write(Z);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GroupPacketMapPing value)
    {
        value = new GroupPacketMapPing();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader) || !reader.TryRead(out value.Guid) || !reader.TryRead(out value.X) || !reader.TryRead(out value.Z))
            return false;

        return reader.RemainingLength == 0;
    }
}
