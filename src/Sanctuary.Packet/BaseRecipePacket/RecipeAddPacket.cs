using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// A recipe learned: the client adds it and prints '"X" recipe learned' itself. It doesn't check for one it already
/// has (it would show twice), and an open recipe window doesn't change until it's shown again.
/// </summary>
public class RecipeAddPacket : BaseRecipePacket, ISerializablePacket
{
    public new const byte OpCode = 1;

    public RecipeData Recipe = new();

    public RecipeAddPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Recipe.Serialize(writer);

        return writer.Buffer;
    }
}
