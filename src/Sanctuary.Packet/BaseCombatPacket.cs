using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The client's CombatProcessor.
/// </summary>
public class BaseCombatPacket
{
    public const short OpCode = 32;

    private short SubOpCode;

    public BaseCombatPacket(short subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }
}
