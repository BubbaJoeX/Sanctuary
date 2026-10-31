namespace Sanctuary.Packet;

/// <summary>
/// The kart garage's vehicle (the client's VehiclePartProcessor): the preview vehicle, and the player's picks, parts
/// equipped and taken off, sent in tunnel 5.
/// </summary>
public class BaseVehiclePartPacket : BaseArenaGamePacket
{
    public const short OpCode = 139;

    public BaseVehiclePartPacket(ushort subOpCode) : base(OpCode, subOpCode)
    {
    }
}
