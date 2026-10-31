namespace Sanctuary.Packet;

/// <summary>
/// Kart racing (the client's VehicleRaceProcessor). The client sends these in tunnel 5, its own kart's state
/// (UpdateVehicleRaceServerData) unreliably.
/// </summary>
public class BaseVehicleRacePacket : BaseArenaGamePacket
{
    public const short OpCode = 33;

    public BaseVehicleRacePacket(ushort subOpCode) : base(OpCode, subOpCode)
    {
    }
}
