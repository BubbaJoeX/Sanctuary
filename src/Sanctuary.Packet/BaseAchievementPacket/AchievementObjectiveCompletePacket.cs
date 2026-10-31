using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// An objective is done (its count is left as it was).
/// </summary>
public class AchievementObjectiveCompletePacket : BaseAchievementPacket, ISerializablePacket
{
    public new const int OpCode = 8;

    public int AchievementId;
    public int ObjectiveId;

    /// <summary>
    /// The achievement's bar, 0 to 1.
    /// </summary>
    public float Progress;

    /// <summary>
    /// A composite effect played on the player; 0 for none (live sent 0).
    /// </summary>
    public int CompositeEffectId;

    public bool Suppress;

    public AchievementObjectiveCompletePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(AchievementId);
        writer.Write(ObjectiveId);
        writer.Write(Progress);
        writer.Write(CompositeEffectId);
        writer.Write(Suppress);

        return writer.Buffer;
    }
}
