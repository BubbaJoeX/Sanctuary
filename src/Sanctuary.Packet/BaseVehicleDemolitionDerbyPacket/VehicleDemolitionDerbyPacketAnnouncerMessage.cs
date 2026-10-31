using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A line in the chat: 0 a power-up taken (Item 3 shield, 4 attack up, 5 ultimate, by Guid), 1 a new leader (Guid), 2 a
/// wreck (Guid the victim, OtherGuid the attacker).
/// </summary>
public class VehicleDemolitionDerbyPacketAnnouncerMessage : BaseVehicleDemolitionDerbyPacket, ISerializablePacket, IDeserializable<VehicleDemolitionDerbyPacketAnnouncerMessage>
{
    public new const ushort OpCode = 20;

    public uint Message;

    public uint Item;

    public ulong Guid;

    public ulong OtherGuid;

    public VehicleDemolitionDerbyPacketAnnouncerMessage() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Message);
        writer.Write(Item);
        writer.Write(Guid);
        writer.Write(OtherGuid);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleDemolitionDerbyPacketAnnouncerMessage value)
    {
        value = new VehicleDemolitionDerbyPacketAnnouncerMessage();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Message))
            return false;

        if (!reader.TryRead(out value.Item))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.OtherGuid))
            return false;

        return reader.RemainingLength == 0;
    }
}
