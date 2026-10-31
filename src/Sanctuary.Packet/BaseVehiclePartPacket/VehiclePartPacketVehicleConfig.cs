using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The garage's preview vehicle: the client replaces the one it shows, and selects the vehicle and type in its list.
/// </summary>
public class VehiclePartPacketVehicleConfig : BaseVehiclePartPacket, ISerializablePacket
{
    public new const ushort OpCode = 2;

    public VehicleConfig Config = null!;

    public List<VehiclePartTexture> PartTextures = [];

    /// <summary>
    /// The loadout selected in the vehicle list.
    /// </summary>
    public uint SelectedVehicleId;

    /// <summary>
    /// 1 karts, 2 derby cars.
    /// </summary>
    public uint VehicleTypeFilter;

    public VehiclePartPacketVehicleConfig() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Config.Write(writer);

        writer.Write(PartTextures.Count);

        foreach (var texture in PartTextures)
            texture.Serialize(writer);

        writer.Write(SelectedVehicleId);
        writer.Write(VehicleTypeFilter);

        return writer.Buffer;
    }
}
