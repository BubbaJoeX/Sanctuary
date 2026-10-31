using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A player reporting another player or a house as offensive, client to server only. Unlike the rating packets the
/// sub-op is a short.
/// </summary>
public class BaseReportPacket
{
    public const short OpCode = 68;

    private short SubOpCode;

    public BaseReportPacket(short subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out short opCode) || opCode != OpCode)
            return false;

        if (!reader.TryRead(out short subOpCode) || subOpCode != SubOpCode)
            return false;

        return true;
    }
}
