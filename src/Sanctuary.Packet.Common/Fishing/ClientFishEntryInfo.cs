using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// One of the pond's fish, as the Fish Finder lists it.
/// </summary>
public class ClientFishEntryInfo : ISerializableType
{
    /// <summary>
    /// Unique per fish; the Fish Finder selects by it.
    /// </summary>
    public int FishType;

    public int NameId;

    /// <summary>
    /// An image set, like an item's icon.
    /// </summary>
    public int IconId;

    /// <summary>
    /// Left out of the Fish Finder.
    /// </summary>
    public bool Hidden;

    public bool Special;

    /// <summary>
    /// The locale name of the lure this fish likes (a lure item's NameId), shown as text.
    /// </summary>
    public int LureNameId;

    /// <summary>
    /// Unused by the client.
    /// </summary>
    public string Unknown = string.Empty;

    /// <summary>
    /// The school the Fish Finder points at for this fish.
    /// </summary>
    public int SchoolId;

    public bool Catchable;

    /// <summary>
    /// Played on that school when the fish is picked in the Fish Finder.
    /// </summary>
    public int FishFinderEffectId;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(FishType);
        writer.Write(NameId);
        writer.Write(IconId);
        writer.Write(Hidden);
        writer.Write(Special);
        writer.Write(LureNameId);
        writer.Write(Unknown);
        writer.Write(SchoolId);
        writer.Write(Catchable);
        writer.Write(FishFinderEffectId);
    }
}
