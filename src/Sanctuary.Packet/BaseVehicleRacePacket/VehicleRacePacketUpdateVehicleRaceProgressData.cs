using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Where a kart stands in the race: the HUD's place, laps and times, the catch-up the client gives a kart behind, and
/// the finish (the client ends its race when its laps reach the total). Live sent it for every kart every 50 ms.
/// </summary>
public class VehicleRacePacketUpdateVehicleRaceProgressData : BaseVehicleRacePacket, ISerializablePacket, IDeserializable<VehicleRacePacketUpdateVehicleRaceProgressData>
{
    public new const ushort OpCode = 14;

    public ulong Guid;

    /// <summary>
    /// 0 is leading.
    /// </summary>
    public uint Place;

    /// <summary>
    /// To the kart ahead, 0 when leading.
    /// </summary>
    public float Gap;

    /// <summary>
    /// float.MaxValue before the start.
    /// </summary>
    public float DistanceToFinish;

    /// <summary>
    /// Laps completed.
    /// </summary>
    public uint Laps;

    public bool Finished;

    public uint TotalLaps;

    /// <summary>
    /// This lap so far, in seconds.
    /// </summary>
    public float LapTime;

    public float LastLapTime;

    public float BestLapTime;

    /// <summary>
    /// The karts in the first four places, the same in every kart's packet of a tick.
    /// </summary>
    public ulong First;

    public ulong Second;

    public ulong Third;

    public ulong Fourth;

    public VehicleRacePacketUpdateVehicleRaceProgressData() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Place);
        writer.Write(Gap);
        writer.Write(DistanceToFinish);
        writer.Write(Laps);
        writer.Write(Finished);
        writer.Write(TotalLaps);
        writer.Write(LapTime);
        writer.Write(LastLapTime);
        writer.Write(BestLapTime);
        writer.Write(First);
        writer.Write(Second);
        writer.Write(Third);
        writer.Write(Fourth);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out VehicleRacePacketUpdateVehicleRaceProgressData value)
    {
        value = new VehicleRacePacketUpdateVehicleRaceProgressData();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Place))
            return false;

        if (!reader.TryRead(out value.Gap))
            return false;

        if (!reader.TryRead(out value.DistanceToFinish))
            return false;

        if (!reader.TryRead(out value.Laps))
            return false;

        if (!reader.TryRead(out value.Finished))
            return false;

        if (!reader.TryRead(out value.TotalLaps))
            return false;

        if (!reader.TryRead(out value.LapTime))
            return false;

        if (!reader.TryRead(out value.LastLapTime))
            return false;

        if (!reader.TryRead(out value.BestLapTime))
            return false;

        if (!reader.TryRead(out value.First))
            return false;

        if (!reader.TryRead(out value.Second))
            return false;

        if (!reader.TryRead(out value.Third))
            return false;

        if (!reader.TryRead(out value.Fourth))
            return false;

        return reader.RemainingLength == 0;
    }
}
