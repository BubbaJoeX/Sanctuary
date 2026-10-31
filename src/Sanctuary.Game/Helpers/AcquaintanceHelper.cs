using System;

using Sanctuary.Game.Entities;
using Sanctuary.Packet;

namespace Sanctuary.Game.Helpers;

/// <summary>
/// The Social window's Recent list: players who grouped or traded with each other show up in each other's.
/// </summary>
public static class AcquaintanceHelper
{
    public static void Met(Player player, Player other)
    {
        if (player.Guid == other.Guid)
            return;

        var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        player.SendTunneled(new AcquaintancePacketAdd { Guid = other.Guid, Name = other.Name.FullName, Timestamp = now, Online = true });
        other.SendTunneled(new AcquaintancePacketAdd { Guid = player.Guid, Name = player.Name.FullName, Timestamp = now, Online = true });
    }
}
