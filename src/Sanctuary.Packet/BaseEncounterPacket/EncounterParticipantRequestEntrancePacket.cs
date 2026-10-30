using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Play on the start panel. The header carries the battle offered.
/// </summary>
public class EncounterParticipantRequestEntrancePacket : BaseEncounterPacket, ISerializablePacket, IDeserializable<EncounterParticipantRequestEntrancePacket>
{
    public new const short OpCode = 108;

    public ulong Guid;

    public EncounterParticipantRequestEntrancePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterParticipantRequestEntrancePacket value)
    {
        value = new EncounterParticipantRequestEntrancePacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        return reader.RemainingLength == 0;
    }
}
