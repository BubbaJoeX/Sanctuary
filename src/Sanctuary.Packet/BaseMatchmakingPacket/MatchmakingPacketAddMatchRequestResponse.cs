using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// The player is in the queue: the Quick Match window shows the request. Any request of theirs already in that queue
/// goes first (each one printing "You have been removed from the matchmaking queue."); one sent by someone else
/// prints "[name] has put you in line for [queue].".
/// </summary>
public class MatchmakingPacketAddMatchRequestResponse : BaseMatchmakingPacket, ISerializablePacket
{
    public new const short OpCode = 4;

    public MatchmakingRequest Request = new();

    public MatchmakingPacketAddMatchRequestResponse() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Request.Serialize(writer);

        return writer.Buffer;
    }
}
