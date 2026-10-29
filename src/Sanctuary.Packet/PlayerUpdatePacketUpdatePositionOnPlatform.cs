using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// What the client sends instead of <see cref="PlayerUpdatePacketUpdatePosition"/> while its player stands
/// on a moving platform (an actor sent with the platform movement type): its update, the platform, and
/// where it stands relative to the platform.
/// </summary>
public struct PlayerUpdatePacketUpdatePositionOnPlatform : IDeserializable<PlayerUpdatePacketUpdatePositionOnPlatform>
{
    public const short OpCode = 185;

    public PlayerUpdatePacketUpdatePosition Update;

    public ulong PlatformGuid;

    /// <summary>
    /// The player's offset from the platform, when the client sent one.
    /// </summary>
    public Vector4? RelativePosition;

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PlayerUpdatePacketUpdatePositionOnPlatform value)
    {
        value = default;

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) || opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.Update.Guid))
            return false;

        if (!reader.TryRead(out value.Update.Position, true))
            return false;

        if (!reader.TryRead(out value.Update.Rotation, true))
            return false;

        if (!reader.TryRead(out value.Update.State))
            return false;

        if (!reader.TryRead(out value.Update.Unknown))
            return false;

        if (!reader.TryRead(out value.PlatformGuid))
            return false;

        if (reader.RemainingLength >= 12)
        {
            if (!reader.TryRead(out Vector4 relativePosition, reader.RemainingLength < 16))
                return false;

            value.RelativePosition = relativePosition with { W = 1f };
        }

        return reader.RemainingLength == 0;
    }

    public override readonly string ToString()
    {
        return $"{Update}, {nameof(PlatformGuid)}: {PlatformGuid}, {nameof(RelativePosition)}: {RelativePosition}";
    }
}
