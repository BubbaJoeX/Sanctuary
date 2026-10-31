using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Houses' ratings for their side panel and the player's house list. The last entry for a house wins.
/// </summary>
public class RatingPacketCandidateInfoReply : BaseRatingPacket, ISerializablePacket
{
    public new const byte OpCode = 17;

    public ulong PlayerGuid;

    public List<RatingCandidateInfo> Candidates = [];

    public RatingPacketCandidateInfoReply() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PlayerGuid);
        writer.Write(Candidates);

        return writer.Buffer;
    }
}
