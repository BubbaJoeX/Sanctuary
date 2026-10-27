using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Sent by the client to invite a player (only <see cref="GroupInviteData.InviteeName"/> filled in), and to
/// the invitee to show the invitation.
/// </summary>
public class GroupPacketGroupInvite : BaseGroupPacket, ISerializablePacket, IDeserializable<GroupPacketGroupInvite>
{
    public new const short OpCode = 1;

    public GroupInviteData Data = new();

    public int[] Unknown = new int[5];

    public GroupPacketGroupInvite() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Data.Serialize(writer);

        foreach (var value in Unknown)
            writer.Write(value);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GroupPacketGroupInvite value)
    {
        value = new GroupPacketGroupInvite();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader) || !value.Data.TryRead(ref reader))
            return false;

        for (var i = 0; i < value.Unknown.Length; i++)
        {
            if (!reader.TryRead(out value.Unknown[i]))
                return false;
        }

        return reader.RemainingLength == 0;
    }
}
