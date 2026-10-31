using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The inventory's Use on an item served as type 28 (a gift to open): the client asks before it opens, and shows the
/// confirm the server answers with (<see cref="ClientUpdatePacketImmediateActivationCheck"/>).
/// </summary>
public class InventoryPacketCheckImmediateActivationItem : BaseInventoryPacket, IDeserializable<InventoryPacketCheckImmediateActivationItem>
{
    public new const short OpCode = 16;

    /// <summary>
    /// The inventory item's id (ClientItem.Id).
    /// </summary>
    public int ItemGuid;

    public InventoryPacketCheckImmediateActivationItem() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out InventoryPacketCheckImmediateActivationItem value)
    {
        value = new InventoryPacketCheckImmediateActivationItem();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader) || !reader.TryRead(out value.ItemGuid))
            return false;

        return reader.RemainingLength == 0;
    }
}
