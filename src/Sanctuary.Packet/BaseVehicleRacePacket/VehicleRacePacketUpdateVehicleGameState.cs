using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The race's game state (2-12): the client acts on each once, then acknowledges it with UpdateVehicleGamePlayerState.
/// 3 waits for the players' karts, 4 for the AI karts, 5 loads and freezes them, 6 frees them and starts the client's
/// kart stream, 8 shows the countdown, 9 is GO, 10 races on (karts still finishing), 11 forces the finish and enables
/// the Exit, 12 removes the player's kart.
/// </summary>
public class VehicleRacePacketUpdateVehicleGameState : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketUpdateVehicleGameState>
{
    public new const ushort OpCode = 1;

    public uint State;

    public VehicleRacePacketUpdateVehicleGameState() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(State);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketUpdateVehicleGameState value)
    {
        value = new VehicleRacePacketUpdateVehicleGameState();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.State))
            return false;

        return reader.RemainingLength == 0;
    }
}
