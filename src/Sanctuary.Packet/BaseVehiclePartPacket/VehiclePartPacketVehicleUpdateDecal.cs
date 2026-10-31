using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Puts a decal (a texture alias) on the garage's preview vehicle.
/// </summary>
public class VehiclePartPacketVehicleUpdateDecal : BaseVehiclePartPacket, ISerializablePacket
{
    public new const ushort OpCode = 4;

    public string Decal = string.Empty;

    public VehiclePartPacketVehicleUpdateDecal() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Decal);

        return writer.Buffer;
    }
}
