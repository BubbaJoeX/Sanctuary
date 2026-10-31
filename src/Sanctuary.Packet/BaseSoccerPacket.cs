namespace Sanctuary.Packet;

/// <summary>
/// Soccer (the client's SoccerProcessor). The client sends these in tunnel 5.
/// </summary>
public class BaseSoccerPacket : BaseArenaGamePacket
{
    public const short OpCode = 76;

    public BaseSoccerPacket(ushort subOpCode) : base(OpCode, subOpCode)
    {
    }
}
