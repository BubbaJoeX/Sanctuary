using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Matchmaking queues: the lobby's queue list (opened from the Quest window's Matchmaking button, or from a game NPC's
/// Lobby option), queueing, the "match found" invitation and the Quick Match window. The client asks for the queue
/// list every time its HUD comes up. Header <c>u16 141, u16 sub</c>; every server packet is read strictly.
/// </summary>
public class BaseMatchmakingPacket
{
    public const short OpCode = 141;

    private short SubOpCode;

    public BaseMatchmakingPacket(short subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out short opCode) || opCode != OpCode)
            return false;

        return reader.TryRead(out short subOpCode) && subOpCode == SubOpCode;
    }
}
