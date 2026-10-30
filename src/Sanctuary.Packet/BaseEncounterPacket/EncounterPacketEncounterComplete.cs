using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// The battle is over: about a second later the client closes the HUD and shows the end screen (the result, the
/// objectives done, the time and the rewards). Live sent it with both header ints 0.
/// </summary>
public class EncounterPacketEncounterComplete : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 3;

    public bool Won;

    /// <summary>
    /// Played on the player; 0 for none.
    /// </summary>
    public int CompositeEffectId;

    public int Stars;

    /// <summary>
    /// Shown as m:ss.
    /// </summary>
    public int ElapsedSeconds;

    public RewardBundleBase Reward = new() { Success = false, IconId = -1, NameId = -1 };

    public RewardBundleBase MemberReward = new() { Success = false, IconId = -1, NameId = -1 };

    /// <summary>
    /// Read but not used.
    /// </summary>
    public RewardBundleBase Unused = new() { Success = false, IconId = -1, NameId = -1 };

    public EncounterPacketEncounterComplete() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Won);
        writer.Write(CompositeEffectId);
        writer.Write(Stars);
        writer.Write(ElapsedSeconds);
        Reward.Serialize(writer);
        MemberReward.Serialize(writer);
        Unused.Serialize(writer);

        return writer.Buffer;
    }
}
