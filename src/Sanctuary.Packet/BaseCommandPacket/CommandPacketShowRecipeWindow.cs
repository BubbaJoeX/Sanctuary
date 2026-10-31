using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Opens the recipe window on the recipes of one kind (only the server can open it). It shows a copy: send it again
/// to show changes.
/// </summary>
public class CommandPacketShowRecipeWindow : BaseCommandPacket, ISerializablePacket
{
    public new const short OpCode = 25;

    /// <summary>
    /// The recipes' FilterType (the crafting job's profile type: 9 smelting, 10 cooking, 12 forging); 0 filters by
    /// <see cref="Group"/> instead, and a negative one shows them all.
    /// </summary>
    public int FilterType;

    /// <summary>
    /// With FilterType 0: the recipes of this group (the farm's orders); 0 or less shows them all.
    /// </summary>
    public int Group;

    public CommandPacketShowRecipeWindow() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(FilterType);
        writer.Write(Group);

        return writer.Buffer;
    }
}
