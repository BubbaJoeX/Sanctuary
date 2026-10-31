namespace Sanctuary.Packet;

/// <summary>
/// The client's Lua timings, sent now and then. Nothing is expected back.
/// </summary>
public static class PacketClientLuaMetrics
{
    public const short OpCode = 142;
}

/// <summary>
/// The client telling the server an upsell nudge was shown or clicked. Nothing is expected back.
/// </summary>
public static class NudgeBasePacket
{
    public const short OpCode = 203;
}
