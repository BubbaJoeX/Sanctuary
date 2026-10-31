using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// A recipe as the recipe window shows it, in SendSelf and the recipe packets. Its name, icon and description are the
/// result item's, and each ingredient's are its item's.
/// </summary>
public class RecipeData : ISerializableType
{
    public int Id { get; set; }

    public int NameId { get; set; }
    public int IconId { get; set; }
    public int IconTint { get; set; }
    public int DescriptionId { get; set; }

    /// <summary>
    /// The tooltip's Quantity (1 in every live recipe).
    /// </summary>
    public int Quantity { get; set; } = 1;

    public bool MembersOnly { get; set; }

    /// <summary>
    /// The crafting job's profile type, which the window filters by: 9 smelting, 10 cooking, 12 forging, 0 farm orders.
    /// </summary>
    public int FilterType { get; set; }

    /// <summary>
    /// At most 8: the window shows no more.
    /// </summary>
    public List<RecipeComponentData> Components { get; set; } = [];

    public int ResultItemId { get; set; }

    public bool PassesRequirement { get; set; } = true;

    /// <summary>
    /// What the window filters farm orders by.
    /// </summary>
    public int Group { get; set; }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(NameId);
        writer.Write(IconId);
        writer.Write(IconTint);
        writer.Write(DescriptionId);
        writer.Write(Quantity);
        writer.Write(MembersOnly);
        writer.Write(FilterType);

        writer.Write(Components);

        // The result comes after the ingredients on the wire, though the client keeps it second.
        writer.Write(ResultItemId);
        writer.Write(PassesRequirement);
        writer.Write(Group);
    }
}

/// <summary>
/// One of a recipe's ingredients. The client counts it only from the stack named here: a stack the server never
/// names counts as none, and Make It stays off.
/// </summary>
public class RecipeComponentData : ISerializableType
{
    public int NameId { get; set; }
    public int IconId { get; set; }
    public int IconTint { get; set; }
    public int DescriptionId { get; set; }

    public int Required { get; set; }

    /// <summary>
    /// The inventory stack (ClientItem.Id) the client counts it from, 0 for none.
    /// </summary>
    public int ItemInstanceId { get; set; }

    /// <summary>
    /// -1 in every live recipe; the client never reads it.
    /// </summary>
    public int Unknown { get; set; } = -1;

    public int ItemId { get; set; }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(NameId);
        writer.Write(IconId);
        writer.Write(IconTint);
        writer.Write(DescriptionId);
        writer.Write(Required);
        writer.Write(ItemInstanceId);
        writer.Write(Unknown);
        writer.Write(ItemId);
    }
}
