using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The client's kart went through a gate. It carries no gate number: the server finds the gate by position.
/// </summary>
public class VehicleRacePacketGateHit : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketGateHit>
{
    public new const ushort OpCode = 7;

    public ulong Guid;

    public Vector4 Position;

    public VehicleRacePacketGateHit() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Position);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketGateHit value)
    {
        value = new VehicleRacePacketGateHit();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Position))
            return false;

        return reader.RemainingLength == 0;
    }
}
