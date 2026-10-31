using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// The Housing Directory's Featured House card. An empty card clears it.
/// </summary>
public class RatingPacketSendFeatured : BaseRatingPacket, ISerializablePacket
{
    public new const byte OpCode = 22;

    public ulong PlayerGuid;

    public string System = HousingSystem;

    public RatingCandidate Candidate = new();

    public RatingPacketSendFeatured() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PlayerGuid);
        writer.Write(System);
        Candidate.Serialize(writer);

        return writer.Buffer;
    }
}
