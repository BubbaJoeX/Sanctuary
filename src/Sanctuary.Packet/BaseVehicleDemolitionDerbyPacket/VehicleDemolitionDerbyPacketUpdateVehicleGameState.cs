using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The derby's game state (2-11): the client acts on each once, then acknowledges it with UpdateVehicleGamePlayerState.
/// 3 waits for the players' cars, 4 for the AI cars, 5 loads and freezes them, 6 starts the client's car stream, 8
/// shows the countdown, 9 is GO, 10 shows the results and 11 enables the Exit.
/// </summary>
public class VehicleDemolitionDerbyPacketUpdateVehicleGameState : BaseVehicleDemolitionDerbyPacket, ISerializablePacket, IDeserializable<VehicleDemolitionDerbyPacketUpdateVehicleGameState>
{
    public new const ushort OpCode = 1;

    public uint State;

    public VehicleDemolitionDerbyPacketUpdateVehicleGameState() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(State);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleDemolitionDerbyPacketUpdateVehicleGameState value)
    {
        value = new VehicleDemolitionDerbyPacketUpdateVehicleGameState();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.State))
            return false;

        return reader.RemainingLength == 0;
    }
}
