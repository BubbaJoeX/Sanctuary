using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The client entering fishing (its fishing state was turned on) and asking for the pond's setup.
/// </summary>
public class FishingPacketRegisterPlayerRequest : BaseFishingPacket, IDeserializable<FishingPacketRegisterPlayerRequest>
{
    public new const ushort OpCode = 2;

    public ulong Guid;

    public FishingPacketRegisterPlayerRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out FishingPacketRegisterPlayerRequest value)
    {
        value = new FishingPacketRegisterPlayerRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        // The client never sets the byte it sends after the guid.
        if (!reader.TryRead(out byte _))
            return false;

        return reader.RemainingLength == 0;
    }
}
