using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A group member accepting or declining a group battle.
/// </summary>
public class EncounterInvitationResponsePacket : BaseEncounterPacket, ISerializablePacket, IDeserializable<EncounterInvitationResponsePacket>
{
    public new const short OpCode = 103;

    public ulong Guid;

    public bool Accept;

    public EncounterInvitationResponsePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Accept);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterInvitationResponsePacket value)
    {
        value = new EncounterInvitationResponsePacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Accept))
            return false;

        return reader.RemainingLength == 0;
    }
}
