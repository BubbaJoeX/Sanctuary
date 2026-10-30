using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Leaving the battle: closing the end screen, or quitting it.
/// </summary>
public class EncounterParticipantRequestExitPacket : BaseEncounterPacket, ISerializablePacket, IDeserializable<EncounterParticipantRequestExitPacket>
{
    public new const short OpCode = 109;

    public EncounterParticipantRequestExitPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterParticipantRequestExitPacket value)
    {
        value = new EncounterParticipantRequestExitPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}
