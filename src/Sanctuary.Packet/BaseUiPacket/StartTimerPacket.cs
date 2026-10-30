using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The minigame HUD's countdown, from Seconds. The client sends nothing when it runs out, so the server keeps
/// the time; sending it again moves the end.
/// </summary>
public class StartTimerPacket : BaseUiPacket, ISerializablePacket
{
    public new const byte OpCode = 10;

    public int Seconds;

    public StartTimerPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Seconds);

        return writer.Buffer;
    }
}
