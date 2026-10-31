using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A power-up on the pitch at (X, 0, Z), or with Kind 0 the removal of the one in Slot. Kinds: 1 Speed Shoes, 2
/// Toughness Charm, 3 MultiBall, 4 Quick Charge, 5 Spring Step.
/// </summary>
public class SoccerPacketSpawnPickUp : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketSpawnPickUp>
{
    public new const ushort OpCode = 9;

    public byte Kind;

    /// <summary>
    /// A Models.txt id.
    /// </summary>
    public uint ModelId;

    public byte Slot;

    public float X;

    public float Z;

    /// <summary>
    /// Seconds.
    /// </summary>
    public float Lifetime;

    /// <summary>
    /// Live 1.
    /// </summary>
    public float Unknown;

    public uint Unknown2;

    public SoccerPacketSpawnPickUp() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Kind);
        writer.Write(ModelId);
        writer.Write(Slot);
        writer.Write(X);
        writer.Write(Z);
        writer.Write(Lifetime);
        writer.Write(Unknown);
        writer.Write(Unknown2);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketSpawnPickUp value)
    {
        value = new SoccerPacketSpawnPickUp();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Kind))
            return false;

        if (!reader.TryRead(out value.ModelId))
            return false;

        if (!reader.TryRead(out value.Slot))
            return false;

        if (!reader.TryRead(out value.X))
            return false;

        if (!reader.TryRead(out value.Z))
            return false;

        if (!reader.TryRead(out value.Lifetime))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        if (!reader.TryRead(out value.Unknown2))
            return false;

        return reader.RemainingLength == 0;
    }
}
