using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The client wants the queue list: whenever its HUD comes back (most window closes) and as the lobby opens.
/// </summary>
public class MatchmakingPacketListQueuesRequest : BaseMatchmakingPacket, IDeserializable<MatchmakingPacketListQueuesRequest>
{
    public new const short OpCode = 1;

    public ulong PlayerGuid;

    public MatchmakingPacketListQueuesRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out MatchmakingPacketListQueuesRequest value)
    {
        value = new MatchmakingPacketListQueuesRequest();

        var reader = new PacketReader(data);

        return value.TryRead(ref reader) && reader.TryRead(out value.PlayerGuid);
    }
}
