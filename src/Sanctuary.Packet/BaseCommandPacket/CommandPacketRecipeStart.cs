using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The recipe window's Make It. The client checks nothing; the server does.
/// </summary>
public class CommandPacketRecipeStart : BaseCommandPacket, IDeserializable<CommandPacketRecipeStart>
{
    public new const short OpCode = 24;

    public int RecipeId;

    public CommandPacketRecipeStart() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out CommandPacketRecipeStart value)
    {
        value = new CommandPacketRecipeStart();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.RecipeId))
            return false;

        return reader.RemainingLength == 0;
    }
}
