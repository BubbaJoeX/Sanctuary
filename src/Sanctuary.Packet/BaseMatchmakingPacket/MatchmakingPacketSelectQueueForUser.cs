using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Opens the Matchmaking lobby on a queue's Quick Play page (or once the queue list arrives, if it hasn't yet). The
/// player still chooses Quick Play or Invite Friends.
/// </summary>
public class MatchmakingPacketSelectQueueForUser : BaseMatchmakingPacket, ISerializablePacket
{
    public new const short OpCode = 12;

    public int QueueId;

    public MatchmakingPacketSelectQueueForUser() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(QueueId);

        return writer.Buffer;
    }
}
