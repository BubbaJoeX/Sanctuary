using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A kart on the grid: its make-up, its grid slot and where it starts. For an AI kart the driver is spawned first, an
/// NPC with the kart's guid. The client's own kart (IsLocalPlayer) makes it acknowledge game state 2 at once.
/// </summary>
public class VehicleRacePacketCreateProxiedVehicleRace : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketCreateProxiedVehicleRace>
{
    public new const ushort OpCode = 11;

    /// <summary>
    /// Comes first on the wire, before the guid.
    /// </summary>
    public VehicleConfig Config = null!;

    /// <summary>
    /// The kart: the character guid for a player, the driver NPC's guid for an AI kart.
    /// </summary>
    public ulong Guid;

    /// <summary>
    /// Rotation rows and position, three floats each.
    /// </summary>
    public Matrix4x4 Spawn = Matrix4x4.Identity;

    /// <summary>
    /// Set only in the copy sent to the kart's own player.
    /// </summary>
    public bool IsLocalPlayer;

    /// <summary>
    /// 0-7: how UpdateVehicleRaceClientData addresses the kart.
    /// </summary>
    public byte GridSlot;

    public VehicleRacePacketCreateProxiedVehicleRace() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Config.Write(writer);
        writer.Write(Guid);
        WriteCompactTransform(writer, Spawn);
        writer.Write(IsLocalPlayer);
        writer.Write(GridSlot);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketCreateProxiedVehicleRace value)
    {
        value = new VehicleRacePacketCreateProxiedVehicleRace();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!VehicleConfig.TryRead(ref reader, out value.Config))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!TryReadCompactTransform(ref reader, out value.Spawn))
            return false;

        if (!reader.TryRead(out value.IsLocalPlayer))
            return false;

        if (!reader.TryRead(out value.GridSlot))
            return false;

        return reader.RemainingLength == 0;
    }
}
