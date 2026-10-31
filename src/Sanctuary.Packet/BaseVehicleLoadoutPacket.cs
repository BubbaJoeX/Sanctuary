using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The player's vehicle loadouts (the client's loadout manager, under BaseClient.Vehicle.LoadoutList). Server to
/// client only, and unlike the other vehicle families the sub-op is a single byte.
/// </summary>
public class BaseVehicleLoadoutPacket
{
    public const short OpCode = 137;

    private readonly byte _subOpCode;

    public BaseVehicleLoadoutPacket(byte subOpCode)
    {
        _subOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(_subOpCode);
    }
}
