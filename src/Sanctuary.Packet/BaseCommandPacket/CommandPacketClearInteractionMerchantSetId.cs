namespace Sanctuary.Packet;

/// <summary>
/// A merchant's window closed (after FreeInteractionNpc). Nothing is expected back.
/// </summary>
public class CommandPacketClearInteractionMerchantSetId : BaseCommandPacket
{
    public new const short OpCode = 43;

    public CommandPacketClearInteractionMerchantSetId() : base(OpCode)
    {
    }
}
