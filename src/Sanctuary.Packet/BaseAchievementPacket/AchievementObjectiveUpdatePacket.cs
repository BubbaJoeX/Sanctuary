using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A tracked objective's new count. An open achievements window shows it the next time it repopulates.
/// </summary>
public class AchievementObjectiveUpdatePacket : BaseAchievementPacket, ISerializablePacket
{
    public new const int OpCode = 7;

    public int AchievementId;
    public int ObjectiveId;

    public int CurrentCount;

    /// <summary>
    /// The achievement's bar, 0 to 1.
    /// </summary>
    public float Progress;

    public AchievementObjectiveUpdatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(AchievementId);
        writer.Write(ObjectiveId);
        writer.Write(CurrentCount);
        writer.Write(Progress);

        return writer.Buffer;
    }
}
