using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The client has finished acting on a game state (sent with that state).
/// </summary>
public class VehicleRacePacketUpdateVehicleGamePlayerState : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketUpdateVehicleGamePlayerState>
{
    public new const ushort OpCode = 2;

    /// <summary>
    /// The player's character.
    /// </summary>
    public ulong Guid;

    public uint State;

    public VehicleRacePacketUpdateVehicleGamePlayerState() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(State);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketUpdateVehicleGamePlayerState value)
    {
        value = new VehicleRacePacketUpdateVehicleGamePlayerState();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.State))
            return false;

        return reader.RemainingLength == 0;
    }
}
