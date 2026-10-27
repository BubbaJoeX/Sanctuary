using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Sent by the leader with the member to remove as the invitee, and to every member (the removed one sees
/// GroupKickedYouOut and loses the group).
/// </summary>
public class GroupPacketGroupKick : BaseGroupPacket, ISerializablePacket, IDeserializable<GroupPacketGroupKick>
{
    public new const short OpCode = 6;

    public GroupInviteData Data = new();

    public GroupPacketGroupKick() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Data.Serialize(writer);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GroupPacketGroupKick value)
    {
        value = new GroupPacketGroupKick();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader) || !value.Data.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}
