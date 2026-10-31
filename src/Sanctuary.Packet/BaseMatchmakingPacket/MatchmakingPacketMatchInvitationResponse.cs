using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The player accepted or declined a found match. A decline also sends the encounter's CancelPending.
/// </summary>
public class MatchmakingPacketMatchInvitationResponse : BaseMatchmakingPacket, IDeserializable<MatchmakingPacketMatchInvitationResponse>
{
    public new const short OpCode = 10;

    public ulong InvitationId;

    public bool Accept;

    public ulong PlayerGuid;

    public MatchmakingPacketMatchInvitationResponse() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out MatchmakingPacketMatchInvitationResponse value)
    {
        value = new MatchmakingPacketMatchInvitationResponse();

        var reader = new PacketReader(data);

        return value.TryRead(ref reader) && reader.TryRead(out value.InvitationId) && reader.TryRead(out value.Accept)
            && reader.TryRead(out value.PlayerGuid);
    }
}
