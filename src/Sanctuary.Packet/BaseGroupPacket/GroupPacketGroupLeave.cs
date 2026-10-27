using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Sent empty by a client leaving its group, and to every member with the one who left. A client that
/// gets its own guid drops the group.
/// </summary>
public class GroupPacketGroupLeave : BaseGroupPacket, ISerializablePacket, IDeserializable<GroupPacketGroupLeave>
{
    public new const short OpCode = 3;

    public ulong Guid;
    public NameData Name = new();

    public GroupPacketGroupLeave() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        Name.Serialize(writer);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GroupPacketGroupLeave value)
    {
        value = new GroupPacketGroupLeave();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader) || !reader.TryRead(out value.Guid) || !value.Name.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}
