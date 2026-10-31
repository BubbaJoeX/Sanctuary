using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// A place in a matchmaking queue, as the client sends it to queue (with no id) and the server sends it back to
/// confirm it (the Quick Match window shows one row per request) or take it away. A request with no participants and
/// no name is 73 bytes.
/// </summary>
public class MatchmakingRequest : ISerializableType
{
    /// <summary>
    /// The server's: the key for cancelling and clearing the request (0 from the client).
    /// </summary>
    public ulong RequestId;

    public int Unknown;

    public int QueueId;

    /// <summary>
    /// Who queued: a group leader queues the whole group, and the others see "[name] has put you in line".
    /// </summary>
    public ulong RequestingPlayerGuid;

    /// <summary>
    /// -1 for a request of one player (the window's "group request" column is anything else).
    /// </summary>
    public int GroupId = -1;

    public int Unknown2;

    /// <summary>
    /// The queue's Param1, copied in by the client.
    /// </summary>
    public int QueueParam1;

    /// <summary>
    /// Unix seconds; the Quick Match window counts the time in the queue from it.
    /// </summary>
    public long CreationTime;

    public bool Unknown3 = true;

    /// <summary>
    /// The requesting player's name (empty from the client).
    /// </summary>
    public NameData PlayerName = new();

    public void Serialize(PacketWriter writer)
    {
        writer.Write(RequestId);
        writer.Write(Unknown);
        writer.Write(QueueId);
        writer.Write(RequestingPlayerGuid);
        writer.Write(GroupId);
        writer.Write(Unknown2);
        writer.Write(QueueParam1);

        // The participants: none.
        writer.Write(0);

        writer.Write(CreationTime);
        writer.Write(Unknown3);

        PlayerName.Serialize(writer);
    }

    /// <summary>
    /// Reads the request as far as its participants, which the server has no use for: the queue and who sent it are
    /// all it needs.
    /// </summary>
    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out RequestId) || !reader.TryRead(out Unknown) || !reader.TryRead(out QueueId)
            || !reader.TryRead(out RequestingPlayerGuid) || !reader.TryRead(out GroupId) || !reader.TryRead(out Unknown2)
            || !reader.TryRead(out QueueParam1))
            return false;

        return true;
    }
}
