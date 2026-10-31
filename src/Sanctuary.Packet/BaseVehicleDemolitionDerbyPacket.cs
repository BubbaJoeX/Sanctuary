namespace Sanctuary.Packet;

/// <summary>
/// The demolition derby (the client's VehicleDemolitionDerbyProcessor). The client sends these in tunnel 5, its own
/// car's state (UpdateVehicleDemolitionDerbyData) unreliably.
/// </summary>
public class BaseVehicleDemolitionDerbyPacket : BaseArenaGamePacket
{
    public const short OpCode = 34;

    public BaseVehicleDemolitionDerbyPacket(ushort subOpCode) : base(OpCode, subOpCode)
    {
    }
}
