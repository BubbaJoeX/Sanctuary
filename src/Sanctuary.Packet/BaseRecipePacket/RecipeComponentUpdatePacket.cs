using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Points one of a recipe's ingredients at the inventory stack the client counts it from (live sent it when a new
/// stack of an ingredient appeared).
/// </summary>
public class RecipeComponentUpdatePacket : BaseRecipePacket, ISerializablePacket
{
    public new const byte OpCode = 2;

    public int RecipeId;

    /// <summary>
    /// The ingredient's place in the recipe's list, from 0.
    /// </summary>
    public int ComponentIndex;

    /// <summary>
    /// The stack's item id (ClientItem.Id), 0 for none.
    /// </summary>
    public int ItemInstanceId;

    public RecipeComponentUpdatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(RecipeId);
        writer.Write(ComponentIndex);
        writer.Write(ItemInstanceId);

        return writer.Buffer;
    }
}
