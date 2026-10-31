using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The client has finished acting on a game state (sent with that state).
/// </summary>
public class VehicleDemolitionDerbyPacketUpdateVehicleGamePlayerState : BaseVehicleDemolitionDerbyPacket, ISerializablePacket, IDeserializable<VehicleDemolitionDerbyPacketUpdateVehicleGamePlayerState>
{
    public new const ushort OpCode = 2;

    /// <summary>
    /// The player's character.
    /// </summary>
    public ulong Guid;

    public uint State;

    public VehicleDemolitionDerbyPacketUpdateVehicleGamePlayerState() : base(OpCode)
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

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleDemolitionDerbyPacketUpdateVehicleGamePlayerState value)
    {
        value = new VehicleDemolitionDerbyPacketUpdateVehicleGamePlayerState();

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
