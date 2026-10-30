using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// What one of the fish round the fisher's lure does: swims up and nibbles, bites, or (neither) swims off, which
/// shows "fish escaped" (or "item lost" for a chest).
/// </summary>
public class FishingPacketUpdateProxiedFishingBobber : BaseFishingPacket, ISerializablePacket
{
    public new const ushort OpCode = 10;

    public ulong Guid;

    /// <summary>
    /// The fish's id in the run (<see cref="Common.UnderwaterFishSpawnInfo.Id"/>).
    /// </summary>
    public int FishId;

    public bool Bite;

    public bool Nibble;

    public FishingPacketUpdateProxiedFishingBobber() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(FishId);
        writer.Write(Bite);
        writer.Write(Nibble);

        return writer.Buffer;
    }
}
