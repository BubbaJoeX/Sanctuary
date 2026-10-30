using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Shows (or takes down) a boss's health bar at the top of the screen. The bar follows the NPC's own hit points,
/// so it must be in view.
/// </summary>
public class CombatPacketEnableBossDisplay : BaseCombatPacket, ISerializablePacket
{
    public new const short OpCode = 9;

    public ulong BossGuid;

    public bool Enable;

    public CombatPacketEnableBossDisplay() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(BossGuid);
        writer.Write(Enable);

        return writer.Buffer;
    }
}
