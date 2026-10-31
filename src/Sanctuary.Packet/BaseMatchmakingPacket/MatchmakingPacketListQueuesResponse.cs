using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// The lobby's queues, the answer to every ListQueuesRequest (sub 1, <c>u64 PlayerGuid</c>). The client replaces its
/// list with them; QueueStatsResponse goes by their order.
/// </summary>
public class MatchmakingPacketListQueuesResponse : BaseMatchmakingPacket, ISerializablePacket
{
    public new const short OpCode = 2;

    /// <summary>
    /// Not read by the client; live echoed the request's.
    /// </summary>
    public ulong Guid;

    public List<MatchmakingQueueDefinition> Queues = [];

    public MatchmakingPacketListQueuesResponse() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Queues);

        return writer.Buffer;
    }
}
