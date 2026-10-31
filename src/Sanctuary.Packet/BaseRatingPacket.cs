using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Ratings (the client's RatingProcessor): the Housing Directory, its votes and its featured house. The sub-op is a
/// byte, and every packet names its rating system, which for houses is <see cref="HousingSystem"/>.
/// </summary>
public class BaseRatingPacket
{
    public const short OpCode = 174;

    /// <summary>
    /// The one rating system the client has. The browser ignores lists for any other.
    /// </summary>
    public const string HousingSystem = "Housing";

    private byte SubOpCode;

    public BaseRatingPacket(byte subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out short opCode) || opCode != OpCode)
            return false;

        if (!reader.TryRead(out byte subOpCode) || subOpCode != SubOpCode)
            return false;

        return true;
    }
}
