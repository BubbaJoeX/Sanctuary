using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// The part of the original server's FishingPlayerConfig the client is sent, in wire order. Names follow the
/// client's own config loader; the fields marked unused are read and never looked at.
/// </summary>
public class FishingPlayerConfig : ISerializableType
{
    /// <summary>
    /// How far out from the fisher the line starts.
    /// </summary>
    public float BobberMinDistanceFromPlayer;

    /// <summary>
    /// The client only lets the fisher cast at water between these distances.
    /// </summary>
    public float MinCastDistance;
    public float MaxCastDistance;

    /// <summary>
    /// The aiming ring's size.
    /// </summary>
    public float MinTargetRadius;
    public float MaxTargetRadius;

    /// <summary>
    /// Unused by the client.
    /// </summary>
    public float PerfectDistance;

    public int BobberSplashCompositeEffectId;

    /// <summary>
    /// Unused by the client.
    /// </summary>
    public float TensionMeterSize;

    /// <summary>
    /// Unused by the client, and not named in its loader.
    /// </summary>
    public float Unknown;

    public float CameraDistance;
    public float CameraPitch;
    public float CameraHeading;
    public float CameraTargetHeight;

    /// <summary>
    /// The original server's bite tuning; the client doesn't read them.
    /// </summary>
    public float FishMaxDistanceFromHook;
    public float FishStartDistanceFromHook;
    public float MaxNibbleTime;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(BobberMinDistanceFromPlayer);
        writer.Write(MinCastDistance);
        writer.Write(MaxCastDistance);
        writer.Write(MinTargetRadius);
        writer.Write(MaxTargetRadius);
        writer.Write(PerfectDistance);
        writer.Write(BobberSplashCompositeEffectId);
        writer.Write(TensionMeterSize);
        writer.Write(Unknown);
        writer.Write(CameraDistance);
        writer.Write(CameraPitch);
        writer.Write(CameraHeading);
        writer.Write(CameraTargetHeight);
        writer.Write(FishMaxDistanceFromHook);
        writer.Write(FishStartDistanceFromHook);
        writer.Write(MaxNibbleTime);
    }
}
