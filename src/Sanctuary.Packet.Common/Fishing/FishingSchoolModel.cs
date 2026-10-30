using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// One model of a fish school on the pond's surface.
/// </summary>
public class FishingSchoolModel : ISerializableType
{
    /// <summary>
    /// A Models.txt id. The client doesn't check it, so an unknown one crashes it.
    /// </summary>
    public int ModelId;

    public float Heading;

    /// <summary>
    /// Passed to the model's first animation; most likely its speed.
    /// </summary>
    public float AnimationParameter;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(ModelId);
        writer.Write(Heading);
        writer.Write(AnimationParameter);
    }
}
