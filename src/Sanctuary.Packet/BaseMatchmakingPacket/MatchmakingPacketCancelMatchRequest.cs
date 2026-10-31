using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The Quick Match window's leave button: the player is out of the queue (the client has dropped the request
/// already).
/// </summary>
public class MatchmakingPacketCancelMatchRequest : BaseMatchmakingPacket, IDeserializable<MatchmakingPacketCancelMatchRequest>
{
    public new const short OpCode = 6;

    public ulong RequestId;

    public MatchmakingPacketCancelMatchRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out MatchmakingPacketCancelMatchRequest value)
    {
        value = new MatchmakingPacketCancelMatchRequest();

        var reader = new PacketReader(data);

        return value.TryRead(ref reader) && reader.TryRead(out value.RequestId);
    }
}
