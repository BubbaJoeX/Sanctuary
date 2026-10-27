using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Player-to-player trades. The client drops any trade packet whose length isn't exactly right.
/// </summary>
public class BaseTradePacket
{
    public const short OpCode = 59;

    private byte SubOpCode;

    public BaseTradePacket(byte subOpCode)
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

        if (!reader.TryRead(out byte subOpCode) || subOpCode != SubOpCode)
            return false;

        return true;
    }
}
