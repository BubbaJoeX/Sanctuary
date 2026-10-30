using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// A fish (or a chest) swimming round the lure in the underwater view.
/// </summary>
public class UnderwaterFishSpawnInfo : ISerializableType
{
    /// <summary>
    /// What the bobber updates call this fish when they make it nibble, bite or swim off.
    /// </summary>
    public int Id;

    /// <summary>
    /// A Models.txt id. The client doesn't check it, so an unknown one crashes it.
    /// </summary>
    public int ModelId;

    public string TextureAlias = string.Empty;
    public string TintAlias = string.Empty;

    /// <summary>
    /// 1 (small) to 4 (extra large): which "!" and "?" the fish shows.
    /// </summary>
    public int SizeClass;

    /// <summary>
    /// Unused by the client.
    /// </summary>
    public float Unknown;

    /// <summary>
    /// A chest rather than a fish. Only honoured on the run's first entry.
    /// </summary>
    public bool IsItem;

    /// <summary>
    /// How the fish swims. The names are lost; these are the client's own defaults.
    /// </summary>
    public float[] SwimParameters = [2f, 2f, 3f, 3f * MathF.PI, 4f, 4f * MathF.PI, 3f, 1.5f, 2f * MathF.PI, 2f, 4f];

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(ModelId);
        writer.Write(TextureAlias);
        writer.Write(TintAlias);
        writer.Write(SizeClass);
        writer.Write(Unknown);
        writer.Write(IsItem);

        foreach (var parameter in SwimParameters)
            writer.Write(parameter);
    }
}
