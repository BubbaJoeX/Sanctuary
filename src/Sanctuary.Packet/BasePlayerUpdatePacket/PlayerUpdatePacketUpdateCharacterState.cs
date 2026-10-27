using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketUpdateCharacterState : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 20;

    public ulong Guid;

    /// <summary>
    /// Status flags, one bit per entry of the client's CharacterStatus* string table, in order: Visible 0x1,
    /// Invisible 0x2, Afraid 0x4, Asleep 0x8, Silenced 0x10, Bound 0x20, Rooted 0x40, Stunned 0x80,
    /// Nonattackable 0x100, GoingHome 0x200, Frozen 0x400, Berserk 0x800, Confused 0x1000, KnockedBack 0x2000,
    /// PoppedUp 0x4000. Only Nonattackable is confirmed (the client's IsNonattackable tests 0x100); its
    /// enemy target checks also skip 0x80 and 0x4000.
    /// </summary>
    public int State;

    public PlayerUpdatePacketUpdateCharacterState() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(State);

        return writer.Buffer;
    }
}
