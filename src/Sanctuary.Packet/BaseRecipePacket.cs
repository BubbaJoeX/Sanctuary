using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Recipes (the client's RecipeManager), server to client only. The sub-op is a byte, and the client drops a recipe
/// packet that's short or has bytes left over.
/// </summary>
public class BaseRecipePacket
{
    public const short OpCode = 63;

    private byte SubOpCode;

    public BaseRecipePacket(byte subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }
}
