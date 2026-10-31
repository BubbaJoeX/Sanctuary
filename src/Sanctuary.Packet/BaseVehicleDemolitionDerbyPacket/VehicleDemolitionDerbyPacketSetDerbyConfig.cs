using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The match's length and the leader's crown.
/// </summary>
public class VehicleDemolitionDerbyPacketSetDerbyConfig : BaseVehicleDemolitionDerbyPacket, ISerializablePacket, IDeserializable<VehicleDemolitionDerbyPacketSetDerbyConfig>
{
    public new const ushort OpCode = 19;

    public float TimeLimitSeconds;

    /// <summary>
    /// A Models.txt id.
    /// </summary>
    public uint CrownModelId;

    public VehicleDemolitionDerbyPacketSetDerbyConfig() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(TimeLimitSeconds);
        writer.Write(CrownModelId);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleDemolitionDerbyPacketSetDerbyConfig value)
    {
        value = new VehicleDemolitionDerbyPacketSetDerbyConfig();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.TimeLimitSeconds))
            return false;

        if (!reader.TryRead(out value.CrownModelId))
            return false;

        return reader.RemainingLength == 0;
    }
}
