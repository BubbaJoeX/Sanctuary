using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// A Housing Directory tab's houses, in order.
/// </summary>
public class RatingPacketDataReply : BaseRatingPacket, ISerializablePacket
{
    public new const byte OpCode = 5;

    public ulong PlayerGuid;

    public string System = HousingSystem;

    public List<RatingCandidate> Candidates = [];

    /// <summary>
    /// Kept by the client, never read.
    /// </summary>
    public int RequestType;

    public RatingPacketDataReply() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PlayerGuid);
        writer.Write(System);
        writer.Write(Candidates.Count);

        // Each card comes after a key the client keeps it by; any value does.
        for (var i = 0; i < Candidates.Count; i++)
        {
            writer.Write(i);
            Candidates[i].Serialize(writer);
        }

        writer.Write(RequestType);

        return writer.Buffer;
    }
}
