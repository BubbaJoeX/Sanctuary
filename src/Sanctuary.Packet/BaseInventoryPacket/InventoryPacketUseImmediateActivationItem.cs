using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// OK on the confirm <see cref="ClientUpdatePacketImmediateActivationCheck"/> opened: open the gift.
/// </summary>
public class InventoryPacketUseImmediateActivationItem : BaseInventoryPacket, IDeserializable<InventoryPacketUseImmediateActivationItem>
{
    public new const short OpCode = 15;

    /// <summary>
    /// The item id the confirm carried.
    /// </summary>
    public int ItemGuid;

    public InventoryPacketUseImmediateActivationItem() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out InventoryPacketUseImmediateActivationItem value)
    {
        value = new InventoryPacketUseImmediateActivationItem();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader) || !reader.TryRead(out value.ItemGuid))
            return false;

        return reader.RemainingLength == 0;
    }
}
