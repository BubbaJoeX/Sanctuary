using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Takes every request of the player's in a queue off the Quick Match window, printing "You have been removed from
/// the matchmaking queue." for each.
/// </summary>
public class MatchmakingPacketRemoveParticipantFromQueueResponse : BaseMatchmakingPacket, ISerializablePacket
{
    public new const short OpCode = 18;

    /// <summary>
    /// Not read by the client.
    /// </summary>
    public ulong Guid;

    public int QueueId;

    public MatchmakingPacketRemoveParticipantFromQueueResponse() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(QueueId);

        return writer.Buffer;
    }
}
