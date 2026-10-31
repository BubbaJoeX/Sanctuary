using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Puts a player at the top of the Recent list (an entry already there moves up), showing how long ago and whether
/// they're online.
/// </summary>
public class AcquaintancePacketAdd : BaseAcquaintancePacket, ISerializablePacket
{
    public new const byte OpCode = 1;

    public ulong Guid;

    public string Name = string.Empty;

    /// <summary>
    /// How they met; the Social movie maps it (its values aren't known, 0 is sent).
    /// </summary>
    public int Type;

    /// <summary>
    /// When, in Unix seconds.
    /// </summary>
    public ulong Timestamp;

    public bool Online;

    public AcquaintancePacketAdd() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Name);
        writer.Write(Type);
        writer.Write(Timestamp);
        writer.Write(Online);

        return writer.Buffer;
    }
}
