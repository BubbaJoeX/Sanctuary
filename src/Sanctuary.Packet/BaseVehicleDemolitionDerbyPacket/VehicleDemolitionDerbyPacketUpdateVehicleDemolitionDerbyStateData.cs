using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A car's health, score and tallies: the only source of them on the client (the HUD, the crown and the results). Live
/// sent it for every car about 20 times a second.
/// </summary>
public class VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyStateData : BaseVehicleDemolitionDerbyPacket, ISerializablePacket, IDeserializable<VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyStateData>
{
    public new const ushort OpCode = 11;

    /// <summary>
    /// The car: the character guid for a player, the driver NPC's guid for an AI car.
    /// </summary>
    public ulong Guid;

    /// <summary>
    /// 0 to 100. The player's own dropping to 0 shows the respawn window, coming back hides it.
    /// </summary>
    public int Health;

    /// <summary>
    /// The car with the most wears the crown.
    /// </summary>
    public int Score;

    /// <summary>
    /// Cars it wrecked.
    /// </summary>
    public int Wrecks;

    public int TimesWrecked;

    public int DamageDealt;

    public int DamageTaken;

    /// <summary>
    /// Body parts still on (1); going to 0 knocks the part off. The client reads them as bools; kept as bytes, since
    /// live sent leftovers in them before the start.
    /// </summary>
    public byte Part;

    public byte Part2;

    public byte Part3;

    /// <summary>
    /// Each corner's dents, 0 to about 0.17.
    /// </summary>
    public float Dent;

    public float Dent2;

    public float Dent3;

    public float Dent4;

    public VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyStateData() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Health);
        writer.Write(Score);
        writer.Write(Wrecks);
        writer.Write(TimesWrecked);
        writer.Write(DamageDealt);
        writer.Write(DamageTaken);
        writer.Write(Part);
        writer.Write(Part2);
        writer.Write(Part3);
        writer.Write(Dent);
        writer.Write(Dent2);
        writer.Write(Dent3);
        writer.Write(Dent4);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyStateData value)
    {
        value = new VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyStateData();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Health))
            return false;

        if (!reader.TryRead(out value.Score))
            return false;

        if (!reader.TryRead(out value.Wrecks))
            return false;

        if (!reader.TryRead(out value.TimesWrecked))
            return false;

        if (!reader.TryRead(out value.DamageDealt))
            return false;

        if (!reader.TryRead(out value.DamageTaken))
            return false;

        if (!reader.TryRead(out value.Part))
            return false;

        if (!reader.TryRead(out value.Part2))
            return false;

        if (!reader.TryRead(out value.Part3))
            return false;

        if (!reader.TryRead(out value.Dent))
            return false;

        if (!reader.TryRead(out value.Dent2))
            return false;

        if (!reader.TryRead(out value.Dent3))
            return false;

        if (!reader.TryRead(out value.Dent4))
            return false;

        return reader.RemainingLength == 0;
    }
}
