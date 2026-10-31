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

/// <summary>
/// Matchmaking queues (the quick match window). The client asks for the queue list every time its HUD comes up;
/// with no queues to offer, it's left unanswered, which the client takes as none.
/// </summary>
public static class BaseMatchmakingPacket
{
    public const short OpCode = 141;
}
