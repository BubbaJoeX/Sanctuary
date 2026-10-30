using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A row of the on-screen objective tracker ("Collect the flyers 3/10"). A minigame's rows go before its
/// StartGame: the tracker only picks up rows that exist when it opens. The text is a locale id; Category is
/// its prefix (1 primary, 2 secondary, 3 job, 4 bonus, 5 elite, 0 none).
/// </summary>
public class TaskAddPacket : BaseUiPacket, ISerializablePacket
{
    public new const byte OpCode = 1;

    public int TaskId;

    public int IconId;

    public int TextId;

    public bool IsBonus;

    public bool MembersOnly;

    public int Category;

    public bool Unknown;

    public int Total;

    public TaskAddPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(TaskId);
        writer.Write(IconId);
        writer.Write(TextId);
        writer.Write(IsBonus);
        writer.Write(MembersOnly);
        writer.Write(Category);
        writer.Write(Unknown);
        writer.Write(Total);

        return writer.Buffer;
    }
}
