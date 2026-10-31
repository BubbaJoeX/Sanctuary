using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Achievements (the client's AchievementManager), server to client only. The sub-op is an int, and the client drops
/// a Complete, ObjectiveAdded, ObjectiveActivated, ObjectiveUpdate or ObjectiveComplete with bytes left over.
/// </summary>
public class BaseAchievementPacket
{
    public const short OpCode = 150;

    private int SubOpCode;

    public BaseAchievementPacket(int subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }
}
