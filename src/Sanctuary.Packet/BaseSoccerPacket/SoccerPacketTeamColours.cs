using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The teams' tints, colours (0x00BBGGRR) and names, for the scoreboard.
/// </summary>
public class SoccerPacketTeamColours : BaseSoccerPacket, ISerializablePacket, IDeserializable<SoccerPacketTeamColours>
{
    public new const ushort OpCode = 8;

    public string Tint = string.Empty;

    public string Tint2 = string.Empty;

    public uint Colour;

    public uint Colour2;

    public uint NameId;

    public uint NameId2;

    public bool Unknown;

    public SoccerPacketTeamColours() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Tint);
        writer.Write(Tint2);
        writer.Write(Colour);
        writer.Write(Colour2);
        writer.Write(NameId);
        writer.Write(NameId2);
        writer.Write(Unknown);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SoccerPacketTeamColours value)
    {
        value = new SoccerPacketTeamColours();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Tint))
            return false;

        if (!reader.TryRead(out value.Tint2))
            return false;

        if (!reader.TryRead(out value.Colour))
            return false;

        if (!reader.TryRead(out value.Colour2))
            return false;

        if (!reader.TryRead(out value.NameId))
            return false;

        if (!reader.TryRead(out value.NameId2))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        return reader.RemainingLength == 0;
    }
}
