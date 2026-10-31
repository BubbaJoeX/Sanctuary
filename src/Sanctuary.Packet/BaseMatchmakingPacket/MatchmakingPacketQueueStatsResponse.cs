using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Each queue's "@1 Waiting" and "Avg Wait", the answer to QueueStatsRequest (sub 13), one value per queue in the
/// order of the last queue list.
/// </summary>
public class MatchmakingPacketQueueStatsResponse : BaseMatchmakingPacket, ISerializablePacket
{
    public new const short OpCode = 14;

    /// <summary>
    /// Not read by the client.
    /// </summary>
    public ulong Guid;

    /// <summary>
    /// Seconds (0 shows "-").
    /// </summary>
    public List<int> AverageWait = [];

    public List<int> PendingRequests = [];

    public MatchmakingPacketQueueStatsResponse() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(AverageWait);
        writer.Write(PendingRequests);

        return writer.Buffer;
    }
}
