using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// An actor's health bar. Other actors' (a mob's) bars show <see cref="HealthPercent"/>; the local player's
/// own bar reads current/max, though <see cref="ClientUpdatePacketHitpoints"/> is what keeps it up to date.
/// </summary>
public class PlayerUpdatePacketUpdateHitpoints : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 5;

    public ulong Guid;

    /// <summary>
    /// 0-100.
    /// </summary>
    public int HealthPercent;

    public int MaxHitpoints;
    public int CurrentHitpoints;

    public PlayerUpdatePacketUpdateHitpoints() : base(OpCode)
    {
    }

    public static int GetHealthPercent(int currentHitpoints, int maxHitpoints) =>
        maxHitpoints <= 0 || currentHitpoints <= 0 ? 0 :
        System.Math.Clamp((int)System.Math.Ceiling(currentHitpoints * 100d / maxHitpoints), 1, 100);

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        writer.Write(HealthPercent);
        writer.Write(MaxHitpoints);
        writer.Write(CurrentHitpoints);

        return writer.Buffer;
    }
}
