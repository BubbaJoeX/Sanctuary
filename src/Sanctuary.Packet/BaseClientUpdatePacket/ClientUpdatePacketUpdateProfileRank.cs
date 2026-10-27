using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Sets a job's rank. When it goes up, the client shows its "JobLevelUp" notification.
/// </summary>
public class ClientUpdatePacketUpdateProfileRank : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 18;

    public int ProfileId;
    public int Rank;

    public ClientUpdatePacketUpdateProfileRank() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ProfileId);
        writer.Write(Rank);

        return writer.Buffer;
    }
}
