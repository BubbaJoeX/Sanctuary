using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The battle is closed for this player: the client drops its state for it (sending RequestExit first if it
/// still had one). Live sent it in answer to RequestExit, before zoning the player back.
/// </summary>
public class EncounterParticipantTerminateEncounterPacket : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 121;

    /// <summary>
    /// Prints "you took too long".
    /// </summary>
    public bool TookTooLong;

    public EncounterParticipantTerminateEncounterPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(TookTooLong);

        return writer.Buffer;
    }
}
