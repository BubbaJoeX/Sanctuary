using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A race announcement, on screen and in chat: "Go!" (<see cref="Type"/> 1), "&lt;name&gt;'s time: 1:23.4 (Personal
/// Best)" (4), or "&lt;name&gt;'s time: 1:23.4". The player named must be in view.
/// </summary>
public class PacketClientAreaTimer : ISerializablePacket
{
    public const short OpCode = 172;

    public const int GoType = 1;
    public const int TimeType = 2;
    public const int PersonalBestType = 4;

    public int Type;

    public int TimeMs;

    public ulong Guid;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);
        writer.Write(Type);
        writer.Write(TimeMs);
        writer.Write(Guid);

        return writer.Buffer;
    }
}
