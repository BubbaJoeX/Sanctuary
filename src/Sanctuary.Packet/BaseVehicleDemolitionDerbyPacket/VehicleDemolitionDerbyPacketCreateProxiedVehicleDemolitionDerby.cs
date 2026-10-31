using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A car in the derby: its make-up and where it starts (and goes back to when it's wrecked). For an AI car the driver
/// is spawned first, an NPC with the car's guid. The client's own car makes it acknowledge game state 2 at once.
/// </summary>
public class VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerby : BaseVehicleDemolitionDerbyPacket, ISerializablePacket, IDeserializable<VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerby>
{
    public new const ushort OpCode = 10;

    /// <summary>
    /// The car: the character guid for a player, the driver NPC's guid for an AI car.
    /// </summary>
    public ulong Guid;

    /// <summary>
    /// Live " " for a player's car and "" for an AI car.
    /// </summary>
    public string Name = string.Empty;

    public VehicleConfig Config = null!;

    /// <summary>
    /// Rotation rows, then the position (w 1).
    /// </summary>
    public Matrix4x4 Spawn = Matrix4x4.Identity;

    /// <summary>
    /// A player's car (game state 3 counts these, 4 the rest).
    /// </summary>
    public bool IsPlayer;

    /// <summary>
    /// Live the time limit's 240 for every car.
    /// </summary>
    public float Unknown = 240f;

    public VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerby() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Name);
        Config.Write(writer);
        WriteTransform(writer, Spawn);
        writer.Write(IsPlayer);
        writer.Write(Unknown);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerby value)
    {
        value = new VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerby();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Name))
            return false;

        if (!VehicleConfig.TryRead(ref reader, out value.Config))
            return false;

        if (!TryReadTransform(ref reader, out value.Spawn))
            return false;

        if (!reader.TryRead(out value.IsPlayer))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        return reader.RemainingLength == 0;
    }
}
