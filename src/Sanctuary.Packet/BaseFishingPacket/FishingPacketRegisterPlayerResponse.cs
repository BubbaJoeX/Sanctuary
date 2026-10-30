using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// The answer to <see cref="FishingPacketRegisterPlayerRequest"/>: how far the fisher can cast, the fishing camera,
/// the pond's underwater view, the models to load ahead and the pond's fish (the Fish Finder's list).
/// </summary>
public class FishingPacketRegisterPlayerResponse : BaseFishingPacket, ISerializablePacket
{
    public new const ushort OpCode = 3;

    public FishingPlayerConfig PlayerConfig = new();
    public FishingZoneConfig ZoneConfig = new();

    /// <summary>
    /// Models.txt ids the client loads now. Unknown ids are skipped.
    /// </summary>
    public List<int> PreloadModelIds = [];

    public List<ClientFishEntryInfo> Fish = [];

    public FishingPacketRegisterPlayerResponse() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        PlayerConfig.Serialize(writer);
        ZoneConfig.Serialize(writer);

        writer.Write(PreloadModelIds);
        writer.Write(Fish);

        return writer.Buffer;
    }
}
