using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Fishing (the client's FishingProcessor). Unlike most base packets the sub-op is a short, and the client drops
/// any fishing packet with bytes left over, so each one is written exactly.
/// </summary>
public class BaseFishingPacket
{
    public const short OpCode = 138;

    private ushort SubOpCode;

    public BaseFishingPacket(ushort subOpCode)
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

        if (!reader.TryRead(out ushort subOpCode) || subOpCode != SubOpCode)
            return false;

        return true;
    }
}
