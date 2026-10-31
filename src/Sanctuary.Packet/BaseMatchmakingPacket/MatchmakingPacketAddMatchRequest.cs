using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Quick Play (or Join! with friends) on a queue's page: the player, or their group, wants a game.
/// </summary>
public class MatchmakingPacketAddMatchRequest : BaseMatchmakingPacket, IDeserializable<MatchmakingPacketAddMatchRequest>
{
    public new const short OpCode = 3;

    public MatchmakingRequest Request = new();

    public MatchmakingPacketAddMatchRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out MatchmakingPacketAddMatchRequest value)
    {
        value = new MatchmakingPacketAddMatchRequest();

        var reader = new PacketReader(data);

        return value.TryRead(ref reader) && value.Request.TryRead(ref reader);
    }
}
