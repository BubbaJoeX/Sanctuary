namespace Sanctuary.Packet;

/// <summary>
/// Sent as the client quits, just before it disconnects; it waits for nothing back.
/// </summary>
public static class PacketClientLogout
{
    public const short OpCode = 16;
}
