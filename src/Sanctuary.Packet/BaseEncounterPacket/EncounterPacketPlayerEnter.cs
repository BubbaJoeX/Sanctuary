using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The player is in the battle: the client starts its clock and opens the HUD and objective tracker. Without it
/// there is no end screen.
/// </summary>
public class EncounterPacketPlayerEnter : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 2;

    /// <summary>
    /// Not read by the client (live sent 0, then the character).
    /// </summary>
    public ulong Guid;

    public bool Unknown;

    public EncounterPacketPlayerEnter() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Unknown);

        return writer.Buffer;
    }
}
