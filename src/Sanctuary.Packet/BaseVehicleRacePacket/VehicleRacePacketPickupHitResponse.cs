using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The item the player's kart got (1 missile, 2 timed effect, 3 mine, 4 shield, 5 boost); the client holds up to three.
/// </summary>
public class VehicleRacePacketPickupHitResponse : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketPickupHitResponse>
{
    public new const ushort OpCode = 18;

    public uint Item;

    public VehicleRacePacketPickupHitResponse() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Item);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketPickupHitResponse value)
    {
        value = new VehicleRacePacketPickupHitResponse();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Item))
            return false;

        return reader.RemainingLength == 0;
    }
}
