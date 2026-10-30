using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Encounters (battles, and the minigames run as encounters). Every one, either way, starts with the sub-op and two
/// ints; the client drops a packet without them.
/// </summary>
public class BaseEncounterPacket
{
    public const short OpCode = 41;

    private short SubOpCode;

    /// <summary>
    /// The encounter instance (live: the instance number in the high 16 bits over the definition id).
    /// </summary>
    public int EncounterId;

    /// <summary>
    /// Echoed back in most of the client's replies (live: always 1008).
    /// </summary>
    public int ReplyId;

    public BaseEncounterPacket(short subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
        writer.Write(EncounterId);
        writer.Write(ReplyId);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out short opCode) || opCode != OpCode)
            return false;

        if (!reader.TryRead(out short subOpCode) || subOpCode != SubOpCode)
            return false;

        return reader.TryRead(out EncounterId) && reader.TryRead(out ReplyId);
    }
}