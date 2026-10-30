using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The knocked out window's answer: respawn, or (Paid) revive where they lie.
/// </summary>
public class EncounterParticipantResumePacket : BaseEncounterPacket, ISerializablePacket, IDeserializable<EncounterParticipantResumePacket>
{
    public new const short OpCode = 122;

    public bool Paid;

    public EncounterParticipantResumePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Paid);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterParticipantResumePacket value)
    {
        value = new EncounterParticipantResumePacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Paid))
            return false;

        return reader.RemainingLength == 0;
    }
}
