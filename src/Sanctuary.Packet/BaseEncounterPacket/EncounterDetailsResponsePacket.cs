using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A battle offer (the start panel, Play disabled until <see cref="EncounterZoneIsReadyPacket"/>), or with
/// <see cref="IsUpdate"/> a refresh of the panel and objectives of the battle the player is in.
/// </summary>
public class EncounterDetailsResponsePacket : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 114;

    public EncounterDetails Details = new();

    public bool IsUpdate;

    /// <summary>
    /// Passed to the panel's marketplace ad (live: the offer's low 16 bits, 0 on updates).
    /// </summary>
    public int MarketplaceId;

    /// <summary>
    /// The panel's BUY area; empty shows none.
    /// </summary>
    public List<int> MarketplaceAds = [];

    public EncounterDetailsResponsePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Details.Serialize(writer);
        writer.Write(IsUpdate);
        writer.Write(MarketplaceId);
        writer.Write(MarketplaceAds);

        return writer.Buffer;
    }
}
