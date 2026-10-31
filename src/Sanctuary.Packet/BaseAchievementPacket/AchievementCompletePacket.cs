using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// An achievement is done: the client stamps its finish time, shows the "achievement unlocked" pop-up and plays the
/// animation on the player. It grants nothing and doesn't fill the bar: send the objectives' completion first.
/// </summary>
public class AchievementCompletePacket : BaseAchievementPacket, ISerializablePacket
{
    public new const int OpCode = 3;

    /// <summary>
    /// emo_achievement_unlocked, what live played.
    /// </summary>
    public const int UnlockedAnimationId = 3370;

    public int AchievementId;

    public bool SuppressToast;

    /// <summary>
    /// An AnimationTypes.xml slot played on the player.
    /// </summary>
    public int AnimationId = UnlockedAnimationId;

    public AchievementCompletePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(AchievementId);
        writer.Write(SuppressToast);
        writer.Write(AnimationId);

        return writer.Buffer;
    }
}
