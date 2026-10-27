using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A hit or a heal: updates the target's health bar and, with <see cref="ShowDelta"/>, floats the number over it
/// for the source and target ("%d", "%d!!" for a crit, "+%d" for a heal). A negative delta also makes the target
/// flinch, and a hit player with nothing targeted targets the attacker.
/// </summary>
public class PlayerUpdatePacketHitPointModification : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 35;

    public ulong SourceGuid;
    public ulong TargetGuid;

    public bool ShowDelta;

    public int MaxHitpoints;
    public int CurrentHitpoints;

    /// <summary>
    /// Negative for damage.
    /// </summary>
    public int Delta;

    public bool IsCrit;

    public PlayerUpdatePacketHitPointModification() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(SourceGuid);
        writer.Write(TargetGuid);

        writer.Write(ShowDelta);

        writer.Write(MaxHitpoints);
        writer.Write(CurrentHitpoints);

        writer.Write(Delta);

        writer.Write(IsCrit);

        return writer.Buffer;
    }
}
