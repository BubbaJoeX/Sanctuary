namespace Sanctuary.Packet;

/// <summary>
/// Sent after every job change (with the job's id first, in SetProfile), to refresh a start screen's prizes. Prizes here don't depend on the job, so nothing changes.
/// </summary>
public class CommandPacketRequestRewardPreviewUpdate : BaseCommandPacket
{
    public new const short OpCode = 37;

    public CommandPacketRequestRewardPreviewUpdate() : base(OpCode)
    {
    }
}
