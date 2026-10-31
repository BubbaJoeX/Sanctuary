using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Every achievement with this character's progress. The client replaces its whole list with it, so it's sent on each
/// zone load, after ClientIsReady (live did the same).
/// </summary>
public class AchievementInitializePacket : BaseAchievementPacket, ISerializablePacket
{
    public new const int OpCode = 4;

    public List<ClientAchievement> Achievements = [];

    public AchievementInitializePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Achievements);

        return writer.Buffer;
    }
}
