using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// What's round the fisher's lure underwater, for the fisher alone. With no fish it puts the lure and the line in
/// (LureModelId); with fish it replaces the ones there, the one the cast will catch first.
/// </summary>
public class FishingPacketSpawnFishRun : BaseFishingPacket, ISerializablePacket
{
    public new const ushort OpCode = 18;

    /// <summary>
    /// Unused by the client.
    /// </summary>
    public bool Unknown;

    /// <summary>
    /// A Models.txt id, only used when <see cref="Fish"/> is empty. An unknown id crashes the client.
    /// </summary>
    public int LureModelId;

    public string LureAlias = string.Empty;

    public List<UnderwaterFishSpawnInfo> Fish = [];

    public FishingPacketSpawnFishRun() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Unknown);
        writer.Write(LureModelId);
        writer.Write(LureAlias);
        writer.Write(Fish);

        return writer.Buffer;
    }
}
