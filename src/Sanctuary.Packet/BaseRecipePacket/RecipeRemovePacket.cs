using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Forgets a recipe.
/// </summary>
public class RecipeRemovePacket : BaseRecipePacket, ISerializablePacket
{
    public new const byte OpCode = 3;

    public int RecipeId;

    public RecipeRemovePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(RecipeId);

        return writer.Buffer;
    }
}
