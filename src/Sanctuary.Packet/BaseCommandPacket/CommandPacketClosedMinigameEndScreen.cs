namespace Sanctuary.Packet;

/// <summary>
/// The minigame end screen closed. Nothing is expected back.
/// </summary>
public class CommandPacketClosedMinigameEndScreen : BaseCommandPacket
{
    public new const short OpCode = 42;

    public CommandPacketClosedMinigameEndScreen() : base(OpCode)
    {
    }
}
