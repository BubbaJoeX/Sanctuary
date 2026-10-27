using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// The invitee's answer, carrying the invitation back. Sent to the inviter when it's a no, so their client
/// shows GroupInviteRejected.
/// </summary>
public class GroupPacketGroupAccept : BaseGroupPacket, ISerializablePacket, IDeserializable<GroupPacketGroupAccept>
{
    public new const short OpCode = 4;

    public GroupInviteData Data = new();

    public bool Accepted;

    public int[] Unknown = new int[5];

    public GroupPacketGroupAccept() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Data.Serialize(writer);

        writer.Write(Accepted);

        foreach (var value in Unknown)
            writer.Write(value);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GroupPacketGroupAccept value)
    {
        value = new GroupPacketGroupAccept();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader) || !value.Data.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Accepted))
            return false;

        for (var i = 0; i < value.Unknown.Length; i++)
        {
            if (!reader.TryRead(out value.Unknown[i]))
                return false;
        }

        return reader.RemainingLength == 0;
    }
}
