using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// How a cast ended. Sent to the fisher and the players around them (who see the catch over their head).
/// </summary>
public class FishingPacketFishingResult : BaseFishingPacket, ISerializablePacket
{
    public new const ushort OpCode = 14;

    public ulong Guid;

    public FishingResultType Type;

    /// <summary>
    /// A chest or a pouch rather than a fish: no size or weight is shown.
    /// </summary>
    public bool IsItem;

    public int NameId;

    /// <summary>
    /// An image set, shown in the catch message and the Fish Finder's catch log.
    /// </summary>
    public int IconId;

    /// <summary>
    /// Unused by the client.
    /// </summary>
    public string Unknown = string.Empty;

    /// <summary>
    /// Shown as "%2.2f" in the catch message.
    /// </summary>
    public float Weight;

    /// <summary>
    /// 1 (small) to 4 (extra large); 0 shows no size.
    /// </summary>
    public int Size;

    /// <summary>
    /// The catch log's columns.
    /// </summary>
    public float TimeToCatch;
    public int Difficulty;
    public int Rarity;
    public int Score;

    /// <summary>
    /// The Models.txt model the fisher holds up. The catch message only shows when it's set; an unknown id
    /// crashes the client.
    /// </summary>
    public int CaughtModelId;

    public string CaughtModelTextureAlias = string.Empty;
    public string CaughtModelTintAlias = string.Empty;

    public int CatchCompositeEffectId;

    /// <summary>
    /// Unused by the client.
    /// </summary>
    public float Unknown2;

    /// <summary>
    /// Back to aiming at once (types 1 and 8).
    /// </summary>
    public bool ResetToIdle;

    /// <summary>
    /// 1 to 4: how big the fish held up is, and how it's held.
    /// </summary>
    public int DisplayScale;

    public FishingPacketFishingResult() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write((int)Type);
        writer.Write(IsItem);
        writer.Write(NameId);
        writer.Write(IconId);
        writer.Write(Unknown);
        writer.Write(Weight);
        writer.Write(Size);
        writer.Write(TimeToCatch);
        writer.Write(Difficulty);
        writer.Write(Rarity);
        writer.Write(Score);
        writer.Write(CaughtModelId);
        writer.Write(CaughtModelTextureAlias);
        writer.Write(CaughtModelTintAlias);
        writer.Write(CatchCompositeEffectId);
        writer.Write(Unknown2);
        writer.Write(ResetToIdle);
        writer.Write(DisplayScale);

        return writer.Buffer;
    }
}

public enum FishingResultType
{
    /// <summary>
    /// "Nothing caught", and back to aiming.
    /// </summary>
    NothingCaught = 0,

    /// <summary>
    /// Ends quietly.
    /// </summary>
    Abort = 1,

    /// <summary>
    /// The fish got away. The "escaped" text comes from the bobber update before it.
    /// </summary>
    Lost = 2,

    /// <summary>
    /// The fish is on the line; the line ties to its mouth and the fisher reels it in.
    /// </summary>
    Hooked = 4,

    /// <summary>
    /// Landed: the fisher holds it up and the catch message shows.
    /// </summary>
    Caught = 5,

    /// <summary>
    /// The cast is called off, back to aiming.
    /// </summary>
    Cancel = 7
}
