using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The Social window's Recent list (the client keeps the last 20), server to client only. The sub-op is a byte.
/// </summary>
public class BaseAcquaintancePacket
{
    public const short OpCode = 72;

    private byte SubOpCode;

    public BaseAcquaintancePacket(byte subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }
}
