using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Sent once as the race starts on the client (character state 0x400): the server answers with the race's setup.
/// </summary>
public class VehicleRacePacketRegisterVehicleRacePlayer : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketRegisterVehicleRacePlayer>
{
    public new const ushort OpCode = 9;

    /// <summary>
    /// The player's character.
    /// </summary>
    public ulong Guid;

    /// <summary>
    /// The client's clock in milliseconds.
    /// </summary>
    public ulong Clock;

    public VehicleRacePacketRegisterVehicleRacePlayer() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Clock);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketRegisterVehicleRacePlayer value)
    {
        value = new VehicleRacePacketRegisterVehicleRacePlayer();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Clock))
            return false;

        return reader.RemainingLength == 0;
    }
}
