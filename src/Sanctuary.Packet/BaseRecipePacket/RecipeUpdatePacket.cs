using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Replaces the client's whole recipe list, each with the stacks its ingredients are counted from now.
/// </summary>
public class RecipeUpdatePacket : BaseRecipePacket, ISerializablePacket
{
    public new const byte OpCode = 5;

    public List<RecipeData> Recipes = [];

    public RecipeUpdatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Recipes.Count);

        // Unlike SendSelf and RecipeAdd, each recipe comes after its key, which must be its id.
        foreach (var recipe in Recipes)
        {
            writer.Write(recipe.Id);
            recipe.Serialize(writer);
        }

        return writer.Buffer;
    }
}
