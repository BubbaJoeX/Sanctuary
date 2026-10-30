using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// The part of the original server's FishingZoneConfig the client is sent, in wire order.
/// </summary>
public class FishingZoneConfig : ISerializableType
{
    /// <summary>
    /// The sky (Resources/SKY) swapped in while the fisher watches their lure underwater.
    /// </summary>
    public string UnderwaterSky = string.Empty;

    /// <summary>
    /// The underwater view is a strip of the zone at y -8, z 485: a cast d metres long shows at
    /// x = <see cref="UnderwaterRunOffset"/> + d * <see cref="UnderwaterRunScale"/>.
    /// </summary>
    public float UnderwaterRunScale;
    public float UnderwaterRunOffset;

    /// <summary>
    /// Unused by the client.
    /// </summary>
    public float ReelDuration;

    /// <summary>
    /// Unused by the client.
    /// </summary>
    public int TensionMeterAmplifier;

    /// <summary>
    /// The lowest a bobber may sit.
    /// </summary>
    public float BobberGroundCheckMinY;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(UnderwaterSky);
        writer.Write(UnderwaterRunScale);
        writer.Write(UnderwaterRunOffset);
        writer.Write(ReelDuration);
        writer.Write(TensionMeterAmplifier);
        writer.Write(BobberGroundCheckMinY);
    }
}
