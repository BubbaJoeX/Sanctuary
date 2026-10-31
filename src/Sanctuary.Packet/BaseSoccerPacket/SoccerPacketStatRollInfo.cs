using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A debug overlay's last tackle roll.
/// </summary>
public class SoccerPacketStatRollInfo : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketStatRollInfo>
{
    public new const ushort OpCode = 17;

    public ulong Guid;

    public uint Stat;

    public uint Roll;

    public ulong OtherGuid;

    public uint OtherStat;

    public uint OtherRoll;

    public uint Result;

    public SoccerPacketStatRollInfo() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Stat);
        writer.Write(Roll);
        writer.Write(OtherGuid);
        writer.Write(OtherStat);
        writer.Write(OtherRoll);
        writer.Write(Result);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketStatRollInfo value)
    {
        value = new SoccerPacketStatRollInfo();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Stat))
            return false;

        if (!reader.TryRead(out value.Roll))
            return false;

        if (!reader.TryRead(out value.OtherGuid))
            return false;

        if (!reader.TryRead(out value.OtherStat))
            return false;

        if (!reader.TryRead(out value.OtherRoll))
            return false;

        if (!reader.TryRead(out value.Result))
            return false;

        return reader.RemainingLength == 0;
    }
}
