using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Puts an item in one slot of one of the player's loadouts (0 empties it).
/// </summary>
public class VehicleUpdateItemPacket : BaseVehicleLoadoutPacket, ISerializablePacket
{
    public new const byte OpCode = 2;

    public uint LoadoutId;
    public uint Slot;
    public uint ItemId;

    public VehicleUpdateItemPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(LoadoutId);
        writer.Write(Slot);
        writer.Write(ItemId);

        return writer.Buffer;
    }
}
