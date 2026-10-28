using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Sanctuary.Game.Conversations;

/// <summary>
/// A GM-authored dialog tree attached to a world NPC, stored as JSON in DbWorldNpc.Conversation.
/// </summary>
public sealed class Conversation
{
    public const int MaxNodes = 64;
    public const int MaxChoicesPerNode = 8;
    public const int MaxTextLength = 400;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public int Start { get; set; } = 1;

    public List<ConversationNode> Nodes { get; set; } = [];

    public ConversationNode? GetNode(int id) => Nodes.FirstOrDefault(x => x.Id == id);

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>
    /// Null for empty or invalid JSON.
    /// </summary>
    public static Conversation? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Conversation>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public Conversation Clone() => FromJson(ToJson())!;

    /// <summary>
    /// Null when the whole tree is usable (as !conv builds it a step at a time), else what's wrong with it.
    /// </summary>
    public string? Validate()
    {
        if (Nodes.Count is 0 or > MaxNodes)
            return $"A conversation has 1 to {MaxNodes} nodes.";

        var ids = Nodes.Select(x => x.Id).ToHashSet();

        if (ids.Count != Nodes.Count || ids.Any(x => x <= 0))
            return "Each node needs its own id above 0.";

        if (!ids.Contains(Start))
            return $"There's no node {Start} to start at.";

        foreach (var node in Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Text) || node.Text.Length > MaxTextLength)
                return $"Node {node.Id} needs text of up to {MaxTextLength} characters.";

            if (node.Choices.Count > MaxChoicesPerNode)
                return $"Node {node.Id} has more than {MaxChoicesPerNode} choices.";

            foreach (var choice in node.Choices)
            {
                if (string.IsNullOrWhiteSpace(choice.Text) || choice.Text.Length > MaxTextLength)
                    return $"Each of node {node.Id}'s choices needs text of up to {MaxTextLength} characters.";

                if (choice.Next != 0 && !ids.Contains(choice.Next))
                    return $"A choice in node {node.Id} goes to node {choice.Next}, which isn't there.";

                if (choice.GiveItemId < 0 || choice.GiveItemId > 0 && choice.GiveQuantity is < 1 or > 100)
                    return $"A choice in node {node.Id} gives 1 to 100 of an item.";
            }
        }

        return null;
    }
}

public sealed class ConversationNode
{
    public int Id { get; set; }

    /// <summary>
    /// What the NPC says.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    public List<ConversationChoice> Choices { get; set; } = [];
}

public sealed class ConversationChoice
{
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Node to go to; 0 ends the conversation.
    /// </summary>
    public int Next { get; set; }

    /// <summary>
    /// Item given when chosen; 0 for none.
    /// </summary>
    public int GiveItemId { get; set; }

    public int GiveQuantity { get; set; } = 1;

    /// <summary>
    /// Passed to the NPC script's onChoice(player, hook) when chosen.
    /// </summary>
    public string? Hook { get; set; }
}
