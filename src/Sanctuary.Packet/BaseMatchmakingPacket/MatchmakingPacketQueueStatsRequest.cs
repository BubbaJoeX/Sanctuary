using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The lobby opened and wants each queue's numbers. Its guid is never set by the client.
/// </summary>
public class MatchmakingPacketQueueStatsRequest : BaseMatchmakingPacket, IDeserializable<MatchmakingPacketQueueStatsRequest>
{
    public new const short OpCode = 13;

    public MatchmakingPacketQueueStatsRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out MatchmakingPacketQueueStatsRequest value)
    {
        value = new MatchmakingPacketQueueStatsRequest();

        var reader = new PacketReader(data);

        return value.TryRead(ref reader);
    }
}
