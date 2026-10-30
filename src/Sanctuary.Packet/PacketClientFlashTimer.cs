using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Starts or stops a count-up stopwatch: the kart race timer (<see cref="Type"/> 0), or the minigame HUD's
/// checkpoint stopwatch for foot races (1).
/// </summary>
public class PacketClientFlashTimer : ISerializablePacket
{
    public const short OpCode = 177;

    public const int CheckpointType = 1;

    public bool Start;

    public int Type;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);
        writer.Write(Start);
        writer.Write(Type);

        return writer.Buffer;
    }
}
