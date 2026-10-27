using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Turns the client's overworld combat mode on or off: together with <see cref="EncounterOverworldCombatPacket"/>
/// it makes hostile NPCs enemies (targetable, enemy nameplates) and shows the game dock's combat indicator.
/// </summary>
public class EncounterPacketIsFighting : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 133;

    public bool IsFighting;

    public EncounterPacketIsFighting() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(IsFighting);

        return writer.Buffer;
    }
}
