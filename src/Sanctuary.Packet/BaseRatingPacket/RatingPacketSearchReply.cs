using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// The Housing Directory's search results.
/// </summary>
public class RatingPacketSearchReply : BaseRatingPacket, ISerializablePacket
{
    public new const byte OpCode = 13;

    public ulong PlayerGuid;

    public string System = HousingSystem;

    public List<RatingCandidate> Candidates = [];

    public RatingPacketSearchReply() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PlayerGuid);
        writer.Write(System);
        writer.Write(Candidates);

        return writer.Buffer;
    }
}
