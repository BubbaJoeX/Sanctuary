using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// One vehicle as the player has kitted it out: an item in each of its 16 slots (0 chassis, 1 engine, 2 turbo,
/// 3 steering, 4 traction, 5 and 6 upgrades, 7 tires, 8 decal, 9 front kit, 10 rear kit, 11 bottle, 12 crystal,
/// 13 horn, 14 tint, 15 gear; 0 for empty). The client writes slots by index with no check against the list's
/// length, so it must always have all 16.
/// </summary>
public class VehicleLoadout : ISerializableType
{
    public const int SlotCount = 16;

    public uint Id;

    /// <summary>
    /// Kept by the client; probably the vehicle type.
    /// </summary>
    public uint Unknown;

    public List<uint> Slots = [.. new uint[SlotCount]];

    public uint Unknown2;
    public uint Unknown3;
    public uint Unknown4;
    public uint Unknown5;
    public uint Unknown6;
    public bool Unknown7;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(Unknown);

        writer.Write(Slots.Count);

        foreach (var slot in Slots)
            writer.Write(slot);

        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
        writer.Write(Unknown5);
        writer.Write(Unknown6);
        writer.Write(Unknown7);
    }
}
