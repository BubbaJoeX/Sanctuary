using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A match was found: the "Join [game]?" notification and the group window's countdown, as for a group battle.
/// Answered with <see cref="MatchmakingPacketMatchInvitationResponse"/>, which carries the invitation id back.
/// </summary>
public class MatchmakingPacketMatchInvitationRequest : BaseMatchmakingPacket, ISerializablePacket
{
    public new const short OpCode = 9;

    /// <summary>
    /// Never 0: the client answers an invitation with no id as an encounter's instead.
    /// </summary>
    public ulong InvitationId;

    /// <summary>
    /// Not read by the client.
    /// </summary>
    public ulong Guid;

    /// <summary>
    /// The game's name (a locale id): the queue's.
    /// </summary>
    public int NameId;

    /// <summary>
    /// Seconds.
    /// </summary>
    public int Timeout;

    public MatchmakingPacketMatchInvitationRequest() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(InvitationId);
        writer.Write(Guid);
        writer.Write(NameId);
        writer.Write(Timeout);

        return writer.Buffer;
    }
}
