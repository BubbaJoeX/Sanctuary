using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Takes a request (by its id) off the Quick Match window, without a word: its game is starting, or it was cancelled.
/// </summary>
public class MatchmakingPacketClearMatchRequest : BaseMatchmakingPacket, ISerializablePacket
{
    public new const short OpCode = 5;

    public MatchmakingRequest Request = new();

    public MatchmakingPacketClearMatchRequest() : base(OpCode)
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
