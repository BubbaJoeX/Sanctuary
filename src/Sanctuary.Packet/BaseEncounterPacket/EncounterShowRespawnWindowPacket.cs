using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The knocked out window: a countdown to respawning and, outside a battle, a paid "Revive here". Answered with
/// <see cref="EncounterParticipantResumePacket"/>.
/// </summary>
public class EncounterShowRespawnWindowPacket : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 125;

    /// <summary>
    /// 0 or less uses the battle's default.
    /// </summary>
    public int Milliseconds;

    /// <summary>
    /// Coins.
    /// </summary>
    public int ReviveCost;

    public EncounterShowRespawnWindowPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Milliseconds);
        writer.Write(ReviveCost);

        return writer.Buffer;
    }
}
