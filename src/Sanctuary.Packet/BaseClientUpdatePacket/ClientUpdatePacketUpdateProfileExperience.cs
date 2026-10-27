using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Refreshes a job's progress in the client's profile list: the same Rank to Unknown20 run that
/// ClientPcProfile carries. The client takes the rank as is and shows no level-up for it
/// (ClientUpdatePacketUpdateProfileRank does that).
/// </summary>
public class ClientUpdatePacketUpdateProfileExperience : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 14;

    public int ProfileId;

    public int Rank;
    public int RankPercent;
    public int StarsAvailable;
    public int StarsEarned;
    public int Unknown20;

    public ClientUpdatePacketUpdateProfileExperience() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ProfileId);

        writer.Write(Rank);
        writer.Write(RankPercent);
        writer.Write(StarsAvailable);
        writer.Write(StarsEarned);
        writer.Write(Unknown20);

        return writer.Buffer;
    }
}
