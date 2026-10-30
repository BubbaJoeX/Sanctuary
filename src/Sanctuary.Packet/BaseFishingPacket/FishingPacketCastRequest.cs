using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The cast reaching the water. The client sends it every frame until the bobber spawns (or the cast is refused),
/// so only the first one of a cast counts.
/// </summary>
public class FishingPacketCastRequest : BaseFishingPacket, IDeserializable<FishingPacketCastRequest>
{
    public new const ushort OpCode = 6;

    public ulong Guid;

    /// <summary>
    /// The point on the water the fisher aimed at.
    /// </summary>
    public Vector4 Target;

    /// <summary>
    /// Always true from this client.
    /// </summary>
    public bool Unknown;

    public FishingPacketCastRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out FishingPacketCastRequest value)
    {
        value = new FishingPacketCastRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Target))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        return reader.RemainingLength == 0;
    }
}
