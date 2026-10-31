using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Starts tracking an objective: its state becomes active and it gets its counts. The client finds it by the key it
/// was listed under, which must be its ObjectiveId.
/// </summary>
public class AchievementObjectiveActivatedPacket : BaseAchievementPacket, ISerializablePacket
{
    public new const int OpCode = 6;

    public int AchievementId;
    public int ObjectiveId;

    public int CurrentCount;
    public int RequiredCount;

    /// <summary>
    /// The achievement's bar, 0 to 1.
    /// </summary>
    public float Progress;

    /// <summary>
    /// Always set live; the client never reads it.
    /// </summary>
    public bool Unknown = true;

    public AchievementObjectiveActivatedPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(AchievementId);
        writer.Write(ObjectiveId);
        writer.Write(CurrentCount);
        writer.Write(RequiredCount);
        writer.Write(Progress);
        writer.Write(Unknown);

        return writer.Buffer;
    }
}
